"""Read only: inspect an existing minidump; print only known JSON exception phrases."""
import pathlib
import re
import struct
import sys
import ctypes
import atexit

data = pathlib.Path(sys.argv[1]).read_bytes()
u32 = lambda at: struct.unpack_from('<I', data, at)[0]
u64 = lambda at: struct.unpack_from('<Q', data, at)[0]
if data[:4] != b'MDMP':
    raise SystemExit('Not a minidump')
streams = {}
for index in range(u32(8)):
    offset = u32(12) + index * 12
    streams[u32(offset)] = (u32(offset + 8), u32(offset + 4))
live_handle = None
if '--pid' in sys.argv:
    live_pid = int(sys.argv[sys.argv.index('--pid') + 1])
    if 15 not in streams or u32(streams[15][0] + 8) != live_pid:
        raise SystemExit('Live read refused: dump process ID does not match the supplied probe PID.')
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.ReadProcessMemory.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
    kernel.ReadProcessMemory.restype = ctypes.c_int
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    live_handle = kernel.OpenProcess(0x1010, False, live_pid)
    if live_handle:
        atexit.register(kernel.CloseHandle, live_handle)
    print('Read-only probe process handle available:', bool(live_handle))
memory = []
modules = []
module_files = {}
if 4 in streams:
    module_list, _ = streams[4]
    for index in range(u32(module_list)):
        item = module_list + 4 + index * 108
        name_rva = u32(item + 20)
        name = data[name_rva + 4:name_rva + 4 + u32(name_rva)].decode('utf-16-le')
        if pathlib.PureWindowsPath(name).name.lower() in ('ovrplugin.dll', 'vcruntime140.dll', 'msvcp140.dll'):
            modules.append((u64(item), u32(item + 8), name))
if 5 in streams:
    offset, _ = streams[5]
    for index in range(u32(offset)):
        item = offset + 4 + index * 16
        memory.append((u64(item), u32(item + 8), u32(item + 12)))
if 9 in streams:
    offset, _ = streams[9]
    file_offset = u64(offset + 8)
    for index in range(u64(offset)):
        item = offset + 16 + index * 16
        start, size = u64(item), u64(item + 8)
        memory.append((start, size, file_offset))
        file_offset += size

def read_virtual(address, count=128):
    for start, size, file_offset in memory:
        if start <= address < start + size:
            offset = file_offset + address - start
            return data[offset:offset + min(count, start + size - address)]
    if live_handle:
        buffer = ctypes.create_string_buffer(count)
        copied = ctypes.c_size_t()
        kernel.ReadProcessMemory(live_handle, address, buffer, count, ctypes.byref(copied))
        if copied.value:
            return buffer.raw[:copied.value]
    for base, image_size, name in modules:
        if base <= address < base + image_size and pathlib.Path(name).exists():
            if name not in module_files:
                module_files[name] = pathlib.Path(name).read_bytes()
            image = module_files[name]
            pe = struct.unpack_from('<I', image, 0x3c)[0]
            number_of_sections = struct.unpack_from('<H', image, pe + 6)[0]
            sections = pe + 24 + struct.unpack_from('<H', image, pe + 20)[0]
            relative_address = address - base
            for index in range(number_of_sections):
                section = sections + index * 40
                virtual_size, virtual_address, raw_size, raw_address = struct.unpack_from('<IIII', image, section + 8)
                if virtual_address <= relative_address < virtual_address + min(virtual_size, raw_size):
                    delta = relative_address - virtual_address
                    offset = raw_address + delta
                    return image[offset:offset + min(count, raw_size - delta)]
    return b''

offset, _ = streams[6]
code = u32(offset + 8)
count = u32(offset + 32)
parameters = [u64(offset + 40 + i * 8) for i in range(min(count, 15))]
print('Exception code:', hex(code))
print('Exception parameter count:', count)
print('Captured memory ranges:', len(memory))
print('Declared captured bytes:', sum(x[1] for x in memory))
print('First range addresses:', [hex(x[0]) for x in memory[:3]])
if code == 0xE06D7363 and len(parameters) >= 3:
    object_data = read_virtual(parameters[1], 128)
    print('Captured exception object bytes:', len(object_data))
    candidates = [object_data]
    for offset in range(0, min(len(object_data), 80) - 7, 8):
        pointer = struct.unpack_from('<Q', object_data, offset)[0]
        candidates.append(read_virtual(pointer, 256))
    known_messages = (b'bad conversion', b'wstring_convert::from_bytes', b'wstring_convert::to_bytes', b'invalid codecvt operation', b'invalid locale name')
    for candidate in candidates:
        message = candidate.split(b'\0')[0]
        if message in known_messages:
            print('Recognized native exception message:', message.decode('ascii'))
    technical_words = {b'Json', b'Value', b'arrayValue', b'objectValue', b'stringValue', b'UInt', b'UInt64', b'LargestUInt', b'not', b'convertible', b'requires', b'string', b'input', b'parse', b'runtime_error', b'invalid', b'argument', b'stoll', b'bad', b'allocation', b'map', b'key', b'null', b'bool', b'object', b'array', b'exception', b'length', b'range'}
    for index, candidate in enumerate(candidates):
        technical = [word.decode('ascii') for word in re.findall(rb'[A-Za-z_][A-Za-z0-9_]*', candidate) if word in technical_words]
        if technical:
            print('Technical exception words, candidate', index, ':', technical)
    if len(parameters) >= 4:
        module_base = parameters[3]
        print('Exception module base:', hex(module_base))
        # SDK85-specific stack evidence supplied by disassembly. Print only the ADB executable path.
        return_address = struct.pack('<Q', module_base + 0x2d21b6)
        adb_found = False
        for start, size, file_offset in memory:
            segment = data[file_offset:file_offset + size]
            for occurrence in re.finditer(re.escape(return_address), segment):
                slot_address = start + occurrence.start()
                saved_register = read_virtual(slot_address - 16, 8)
                if len(saved_register) != 8:
                    continue
                string_object = read_virtual(struct.unpack('<Q', saved_register)[0], 32)
                if len(string_object) != 32:
                    continue
                command_size, capacity = struct.unpack_from('<QQ', string_object, 16)
                if not (0 < command_size <= 4096 and command_size <= capacity):
                    continue
                command = string_object[:command_size] if capacity <= 15 else read_virtual(struct.unpack_from('<Q', string_object)[0], command_size)
                if b'devices -l' not in command or b'adb' not in command.lower():
                    continue
                decoded = command.decode('mbcs', errors='replace')
                executable = re.search(r'(?i)[A-Z]:[\\/][^\r\n\x00"]*?adb\.exe', decoded)
                if executable:
                    safe_path = re.sub(r'(?i)C:[\\/]Users[\\/][^\\/]+', 'C:/Users/<user>', executable.group())
                    print('Native ADB executable path:', safe_path)
                    print('Native ADB arguments: devices -l')
                    adb_found = True
        if not adb_found:
            print('Native ADB command heap was not available in this snapshot.')
        throw_info = read_virtual(parameters[2], 16)
        if len(throw_info) == 16:
            catch_array = read_virtual(module_base + struct.unpack_from('<I', throw_info, 12)[0], 8)
            if len(catch_array) == 8:
                catch_type = read_virtual(module_base + struct.unpack_from('<I', catch_array, 4)[0], 28)
                if len(catch_type) == 28:
                    type_data = read_virtual(module_base + struct.unpack_from('<I', catch_type, 4)[0] + 16, 96).split(b'\0')[0]
                    if re.fullmatch(rb'[.?@A-Za-z0-9_$]+', type_data):
                        print('C++ exception type:', type_data.decode('ascii'))
    pattern = rb'(?:Value is not convertible to [A-Za-z0-9_ ]+[.]|in Json::[A-Za-z0-9_:(). >-]+|requires (?:object|array)Value|[A-Za-z0-9_:() ]+requires [A-Za-z0-9_ ]+)'
    messages = {match.group().decode('ascii', 'replace') for candidate in candidates for match in re.finditer(pattern, candidate)}
    if messages:
        for message in sorted(messages):
            print('JSON exception message:', message)
    else:
        print('Exception message was not captured by this limited reader.')

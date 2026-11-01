"""Select the current Steam input by identity, never a cached index."""
import json

def select_input(devices, apis):
    choices = [(i, d) for i, d in enumerate(devices)
               if d['max_input_channels'] > 0 and 'Steam Streaming Microphone' in d['name']]
    if not choices:
        raise RuntimeError('Steam microphone unavailable; connect Steam Link first')
    i, d = min(choices, key=lambda item: 0 if apis[item[1]['hostapi']]['name'] == 'Windows WASAPI' else 1)
    return {'device': i, 'rate': int(d['default_samplerate'])}

if __name__ == '__main__':
    import sounddevice as sd
    print(json.dumps(select_input(sd.query_devices(), sd.query_hostapis())))

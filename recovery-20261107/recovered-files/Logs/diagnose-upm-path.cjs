// Read-only diagnostic: load the installed UPM context code without starting its server.
const fs = require('node:fs');
const Module = require('node:module');
const sourceFile = 'D:/unity_hub/6000.3.10f1/Editor/Data/Resources/PackageManager/Server/app/app.js';
const source = fs.readFileSync(sourceFile, 'utf8');
const marker = 'var import_node_process=__toESM(require("node:process"))';
const end = source.indexOf(marker);
if (end < 0) throw new Error('Unexpected UPM entry point; no source files changed.');
const diagnosticModule = new Module(sourceFile, module);
diagnosticModule.filename = sourceFile;
diagnosticModule.paths = Module._nodeModulePaths(require('node:path').dirname(sourceFile));
diagnosticModule._compile(source.slice(0, end) + ';module.exports=require_operation_context();', sourceFile);
diagnosticModule.exports.initContext({
  projectPath: 'D:/UnityProjects/NarutoVR/Packages',
  editorPath: 'D:/unity_hub/6000.3.10f1/Editor/Data/Resources/PackageManager/Editor',
  builtInPackagesPath: ['D:/unity_hub/6000.3.10f1/Editor/Data/Resources/PackageManager/BuiltInPackages'],
  unityVersion: '6000.3.10f1'
}).then(() => console.log('UPM_CONTEXT_OK')).catch(error => {
  console.error(error.stack);
  process.exitCode = 1;
});

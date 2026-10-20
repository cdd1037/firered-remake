"""Packaging contract tests only: never invokes Godot, publishes, or exports."""
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('windows_export', ROOT / 'scripts/lib/windows_export.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


def pe(path, machine=0x8664):
    data = bytearray(128)
    data[:2] = b'MZ'
    struct.pack_into('<I', data, 0x3c, 64)
    data[64:68] = b'PE\0\0'
    struct.pack_into('<H', data, 68, machine)
    path.write_bytes(data)


class WindowsExportTests(unittest.TestCase):
    def test_x64_pe(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / 'test.exe'; pe(p); m.require_x64_pe(p)
            pe(p, 0x14c)
            with self.assertRaises(RuntimeError): m.require_x64_pe(p)

    def test_missing_template_does_not_run(self):
        with tempfile.TemporaryDirectory() as td:
            r = Path(td)
            with self.assertRaises(FileNotFoundError):
                m.export_windows(r, {'XDG_DATA_HOME': td, 'APPDATA': td}, lambda *a: self.fail('must not run'), r/'godot')

    def fixture(self, root):
        data = root/'data'; data.mkdir()
        settings = {n: True for n in ['System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault', 'System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting']}
        config = {'runtimeOptions': {'configProperties': settings, 'includedFrameworks': [{'name': 'Microsoft.NETCore.App', 'version': '10.0.12'}]}}
        (data/'Game.Godot.runtimeconfig.json').write_text(json.dumps(config))
        original = root/'nuget/microsoft.netcore.app.runtime.win-x64/10.0.12/runtimes/win-x64/lib/net10.0/System.Private.CoreLib.dll'
        original.parent.mkdir(parents=True); original.write_bytes(b'original-longer')
        linked = root/'src/Game.Godot/.godot/mono/temp/obj/ExportRelease/win-x64/linked/System.Private.CoreLib.dll'
        linked.parent.mkdir(parents=True); linked.write_bytes(b'linked')
        (data/'System.Private.CoreLib.dll').write_bytes(b'linked')
        for n in ['Game.Godot','Game.Application','Game.Content','Game.Domain','Game.Audio','GodotSharp','System.Collections','System.Collections.Immutable']:
            (data/(n+'.dll')).write_bytes(b'fixture')
        for n in ['coreclr','hostfxr','hostpolicy']: pe(data/(n+'.dll'))
        return data, {'NUGET_PACKAGES':str(root/'nuget')}, config

    def test_actual_link_match(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); d,e,_=self.fixture(r)
            self.assertTrue(m.verify_trimmed_runtime(r,d,e)['publishTrimmed'])
            (d/'System.Private.CoreLib.dll').write_bytes(b'other')
            with self.assertRaisesRegex(RuntimeError,'ILLink output'): m.verify_trimmed_runtime(r,d,e)

    def test_reflection_required(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); d,e,c=self.fixture(r)
            c['runtimeOptions']['configProperties']['System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault']=False
            (d/'Game.Godot.runtimeconfig.json').write_text(json.dumps(c))
            with self.assertRaisesRegex(RuntimeError,'switch missing'): m.verify_trimmed_runtime(r,d,e)

    def test_collections_required(self):
        with tempfile.TemporaryDirectory() as td:
            r=Path(td); d,e,_=self.fixture(r); (d/'System.Collections.dll').unlink()
            with self.assertRaisesRegex(RuntimeError,'Missing bundled'): m.verify_trimmed_runtime(r,d,e)

    def test_trim_is_scoped(self):
        x=ET.parse(ROOT/'src/Game.Godot/Game.Godot.csproj').getroot()
        g=next(g for g in x.findall('PropertyGroup') if g.find('FireRedTrimmedExport') is not None)
        self.assertIn("'win-x64'",g.attrib['Condition']); self.assertIn("'ExportRelease'",g.attrib['Condition'])
        self.assertEqual('All',x.find("Target/ItemGroup/TrimmerRootAssembly").attrib['RootMode'])
        self.assertEqual('PrepareForILLink',x.find('Target').attrib['AfterTargets'])

    def test_bounded_descriptor(self):
        x=ET.parse(ROOT/'src/Game.Godot/TrimReflectionRoots.xml').getroot()
        self.assertEqual({'System.Collections.Generic.Dictionary`2','System.Collections.Generic.List`1','System.Collections.Generic.HashSet`1'}, {n.attrib['fullname'] for n in x.findall('assembly/type')})
        self.assertEqual(3,len(x.findall('assembly/type')))

    def test_opt_in_official_tasks(self):
        x=ET.parse(ROOT/'scripts/dev/il-link-in-process.targets').getroot()
        self.assertEqual({'ILLink','ComputeManagedAssemblies'},{n.attrib['TaskName'] for n in x.findall('UsingTask')})
        for n in x.findall('UsingTask'):
            self.assertEqual('true',n.attrib['Override']); self.assertEqual('$(ILLinkTasksAssembly)',n.attrib['AssemblyFile']); self.assertEqual('AssemblyTaskFactory',n.attrib['TaskFactory'])


if __name__ == '__main__': unittest.main()

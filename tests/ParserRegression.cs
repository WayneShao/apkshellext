using System;
using System.IO;
using System.Reflection;
using System.IO.Compression;
using System.Drawing;
using System.Text;
using System.Runtime.Serialization;
using ApkQuickReader;
class ParserRegression {
 static object Call(ApkReader r,string name,Type[] types,params object[] args) { return typeof(ApkReader).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic,null,types,null).Invoke(r,args); }
 static byte[] Xml(bool cycle=false) {
  string[] strings=cycle?new[]{"layer-list","drawable","cycle.xml","item"}:new[]{"manifest","package","example.fixture"};
  var pool=new MemoryStream(); var w=new BinaryWriter(pool); w.Write((ushort)1);w.Write((ushort)28);w.Write(0);w.Write(strings.Length);w.Write(0);w.Write(0);w.Write(28+strings.Length*4);w.Write(0);
  int offset=0;foreach(string str in strings){w.Write(offset);offset+=2+str.Length*2+2;} foreach(string str in strings){w.Write((ushort)str.Length);w.Write(Encoding.Unicode.GetBytes(str));w.Write((ushort)0);} long len=pool.Length;pool.Position=4;w.Write((int)len);
  var result=new MemoryStream();w=new BinaryWriter(result);w.Write((ushort)3);w.Write((ushort)8);w.Write((int)(8+len+56+24+(cycle?80:0)));w.Write(pool.ToArray());
  w.Write((ushort)0x102);w.Write((ushort)16);w.Write(56);w.Write(1);w.Write(-1);w.Write(-1);w.Write(0);w.Write((ushort)20);w.Write((ushort)20);w.Write((ushort)1);w.Write((ushort)0);w.Write(0);w.Write(-1);w.Write(1);w.Write(-1);w.Write((ushort)8);w.Write((byte)0);w.Write((byte)3);w.Write(2);
  if(cycle){byte[] root=result.ToArray(); byte[] item=new byte[56];Array.Copy(root,8+len,item,0,56);BitConverter.GetBytes(3).CopyTo(item,20);w.Write(item);w.Write((ushort)0x103);w.Write((ushort)16);w.Write(24);w.Write(1);w.Write(-1);w.Write(-1);w.Write(3);}
  w.Write((ushort)0x103);w.Write((ushort)16);w.Write(24);w.Write(1);w.Write(-1);w.Write(-1);w.Write(0);return result.ToArray();
 }
 static string Fixture(bool manifest) {
  string path=Path.GetTempFileName(); using(var file=File.Create(path)) using(var zip=new ZipArchive(file,ZipArchiveMode.Create)) {
   using(var output=zip.CreateEntry("icon.png").Open()) using(var bmp=new Bitmap(2,2)) {bmp.SetPixel(0,0,Color.Red);bmp.Save(output,System.Drawing.Imaging.ImageFormat.Png);}
   using(var output=zip.CreateEntry("cycle.xml").Open()){byte[] xml=Xml(true);output.Write(xml,0,xml.Length);}
   if(manifest) using(var output=zip.CreateEntry("AndroidManifest.xml").Open()){byte[] xml=Xml();output.Write(xml,0,xml.Length);}
  } return path;
 }
 static byte[] Pool(params string[] values) {
  using(var ms=new MemoryStream()) {var w=new BinaryWriter(ms);w.Write((ushort)1);w.Write((ushort)28);w.Write(0);w.Write(values.Length);w.Write(0);w.Write(0);w.Write(28+values.Length*4);w.Write(0);int offset=0;
   foreach(string value in values){w.Write(offset);offset+=4+value.Length*2;}foreach(string value in values){w.Write((ushort)value.Length);w.Write(Encoding.Unicode.GetBytes(value));w.Write((ushort)0);}while(ms.Length%4!=0)w.Write((byte)0);long size=ms.Length;ms.Position=4;w.Write((int)size);return ms.ToArray();}
 }
 static byte[] Table(int mode,bool compact,int typeOffset,out int typeStart,out int entryStart) {
  byte[] global=Pool("Modern App","icon.png"),types=Pool("string"),keys=Pool("label","icon","alias");
  int stride=compact?8:16,count=mode==1?3:10,mapSize=count*(mode==2?2:4),start=48+mapSize;
  byte[] type;using(var ms=new MemoryStream()){var w=new BinaryWriter(ms);w.Write((ushort)0x201);w.Write((ushort)48);w.Write(start+stride*3);w.Write((byte)1);w.Write((byte)mode);w.Write((ushort)0);w.Write(count);w.Write(start);w.Write(28);w.Write(new byte[24]);
   if(mode==1){int[] ids={2,7,9};for(int i=0;i<3;i++){w.Write((ushort)ids[i]);w.Write((ushort)(i*stride/4));}}
   else for(int i=0;i<count;i++){uint off=i==2?0:i==7?(uint)stride:i==9?(uint)(stride*2):uint.MaxValue;if(mode==2)w.Write(off==uint.MaxValue?ushort.MaxValue:(ushort)(off/4));else w.Write(off);}
   for(int i=0;i<3;i++){byte kind=(byte)(i==2?1:3);uint data=i==2?(uint)(0x7f000002|((1+typeOffset)<<16)):(uint)i;if(compact){w.Write((ushort)i);w.Write((ushort)(8|(kind<<8)));w.Write(data);}else{w.Write((ushort)8);w.Write((ushort)0);w.Write(i);w.Write((ushort)8);w.Write((byte)0);w.Write(kind);w.Write(data);}}
   type=ms.ToArray();}
  typeStart=12+global.Length+288+types.Length+keys.Length;entryStart=typeStart+start;
  using(var ms=new MemoryStream()){var w=new BinaryWriter(ms);w.Write((ushort)2);w.Write((ushort)12);w.Write(typeStart+type.Length);w.Write(1);w.Write(global);w.Write((ushort)0x200);w.Write((ushort)288);w.Write(288+types.Length+keys.Length+type.Length);w.Write(0x7f);w.Write(new byte[256]);w.Write(288);w.Write(1);w.Write(288+types.Length);w.Write(3);w.Write(typeOffset);w.Write(types);w.Write(keys);w.Write(type);return ms.ToArray();}
 }
 static ApkResource Resource(ApkReader reader,byte[] table,uint id) {
  typeof(ApkReader).GetField("resources",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(reader,table);
  return (ApkResource)Call(reader,"QuickSearchResource",new[]{typeof(uint)},id);
 }
 static void Modern(int mode,int typeOffset) {
  string path=Fixture(true);try{using(var reader=new ApkReader(path)){foreach(bool compact in new[]{false,true}){
   int type,entry;byte[] table=Table(mode,compact,typeOffset,out type,out entry);uint root=(uint)(0x7f000000|((1+typeOffset)<<16));
   var name=Resource(reader,table,root|2);if(name.Count!=1 || (string)name.defaultValue!="Modern App")throw new Exception("Modern name failed mode="+mode+" compact="+compact);
   var icon=Resource(reader,table,root|7);if(icon.Count!=1 || (string)icon.defaultValue!="icon.png")throw new Exception("Modern icon path failed");using(var bmp=reader.getImage((string)icon.defaultValue,new Size(16,16)))if(bmp==null)throw new Exception("Modern icon decode failed");
   if((string)Resource(reader,table,root|9).defaultValue!="Modern App")throw new Exception("Modern reference failed");
   if(Resource(reader,table,root|3).Count!=0 || Resource(reader,table,root|15).Count!=0)throw new Exception("NO_ENTRY lookup failed");
  }}}finally{File.Delete(path);}
 }
 static void SparseMaximumOffset() {
  int type,entry;byte[] source=Table(1,true,0,out type,out entry);byte[] table=new byte[entry+65535*4+8];Array.Copy(source,table,source.Length);Array.Copy(source,entry,table,entry+65535*4,8);
  int package=12+Pool("Modern App","icon.png").Length;BitConverter.GetBytes(table.Length).CopyTo(table,4);BitConverter.GetBytes(table.Length-package).CopyTo(table,package+4);BitConverter.GetBytes(table.Length-type).CopyTo(table,type+4);BitConverter.GetBytes(ushort.MaxValue).CopyTo(table,type+50);
  string path=Fixture(true);try{using(var reader=new ApkReader(path))if((string)Resource(reader,table,0x7f010002).defaultValue!="Modern App")throw new Exception("Sparse 0xffff offset is valid, not NO_ENTRY");}finally{File.Delete(path);}
 }
 static void ModernErrors() {
  string path=Fixture(true);try{using(var reader=new ApkReader(path)){for(int mode=0;mode<3;mode++)foreach(bool compact in new[]{false,true})for(int fault=0;fault<7;fault++){
   int type,entry;byte[] table=Table(mode,compact,0,out type,out entry);
   switch(fault){
    case 0: BitConverter.GetBytes(uint.MaxValue).CopyTo(table,type+12);break;
    case 1: BitConverter.GetBytes((uint)48).CopyTo(table,type+16);break;
    case 2: if(mode==1)BitConverter.GetBytes(ushort.MaxValue).CopyTo(table,type+50);else if(mode==2)BitConverter.GetBytes((ushort)0xfffe).CopyTo(table,type+52);else BitConverter.GetBytes(uint.MaxValue-1).CopyTo(table,type+56);break;
    case 3: if(mode==1)BitConverter.GetBytes((ushort)2).CopyTo(table,type+52);else table[type+9]=3;break;
    case 4: if(compact)table[entry+2]|=1;else BitConverter.GetBytes((ushort)0xffff).CopyTo(table,entry);break;
    case 5: BitConverter.GetBytes((ushort)20).CopyTo(table,type+2);break;
    case 6: int package=12+Pool("Modern App","icon.png").Length;BitConverter.GetBytes((uint)255).CopyTo(table,package+284);break;
   }
   try{Resource(reader,table,0x7f010002);throw new Exception("Malformed modern table accepted mode="+mode+" compact="+compact+" fault="+fault);}catch(TargetInvocationException ex){if(!(ex.InnerException is InvalidDataException))throw;}
  }}}finally{File.Delete(path);}
 }
 static int Main(string[] args) {
  var r=(ApkReader)FormatterServices.GetUninitializedObject(typeof(ApkReader)); GC.SuppressFinalize(r);
  try {
   if(args[0]=="modern_sparse_max"){SparseMaximumOffset();}
   else if(args[0]=="modern_type_offset"){for(int mode=0;mode<3;mode++)Modern(mode,5);}
   else if(args[0]=="modern_errors"){ModernErrors();}
   else if(args[0].StartsWith("modern_")){Modern(args[0]=="modern_sparse"?1:args[0]=="modern_offset16"?2:0,args[0]=="modern_type_offset"?5:0);}
   else if(args[0]=="legacy_type") {byte[] data={3,0,8,0,8,0,0,0};if(!(ApkShellext2.ApkChunk.ApkChunk.ReadChunkFile(ref data) is ApkShellext2.ApkChunk.ApkXMLChunk))throw new Exception("Legacy XML type decoding failed");}
   else if(args[0]=="real") {using(var reader=new ApkReader(args[1])) {Console.WriteLine(reader.PackageName+" | "+reader.AppName+" | "+reader.Version);if(String.IsNullOrEmpty(reader.PackageName))throw new Exception("Missing package");using(var bmp=reader.getIcon(new Size(64,64)))if(bmp==null)throw new Exception("Missing icon");}}
   else if(args[0]=="staged_download") {
    string fixture=Fixture(true);byte[] complete=File.ReadAllBytes(fixture);string path=fixture+".apk";
    try {for(int round=0;round<5;round++) {
     foreach(byte[] stage in new[]{new byte[0],new byte[]{80,75,3,4,0,0}}) {File.WriteAllBytes(path,stage);try{using(var reader=new ApkReader(path)){var name=reader.PackageName;}throw new Exception("Partial ZIP accepted");}catch(Exception e){if(e.Message=="Partial ZIP accepted")throw;}}
     File.WriteAllBytes(path,complete);using(var reader=new ApkReader(path)){if(reader.PackageName!="example.fixture")throw new Exception("Completion not parsed");using(var bmp=reader.getImage("icon.png",new Size(16,16)))if(bmp==null)throw new Exception("Completed icon missing");}
    }}finally{File.Delete(path);File.Delete(fixture);}
   }
   else if(args[0]=="cycle") {string path=Fixture(true);try{using(var reader=new ApkReader(path)){try{reader.getImage("cycle.xml",new Size(16,16));throw new Exception("Cycle accepted");}catch(InvalidDataException){} using(var bmp=reader.getImage("icon.png",new Size(16,16)))if(bmp==null)throw new Exception("Cycle guard poisoned later read");}}finally{File.Delete(path);}}
   else if(args[0]=="resource_failure") {
    var stack=new System.Collections.Generic.Stack<uint>();typeof(ApkReader).GetField("searchstack",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(r,stack);
    typeof(ApkReader).GetField("resources",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(r,new byte[]{2,0,12,0,20,0,0,0,1,0,0,0,255,127,8,0,0,0,0,0});
    for(int i=0;i<2;i++){try{Call(r,"QuickSearchResource",new[]{typeof(uint)},(uint)0x7f010000);throw new Exception("Invalid resource chunk accepted");}catch(TargetInvocationException e){if(!(e.InnerException is InvalidDataException))throw;}if(stack.Count!=0)throw new Exception("Resource stack leaked");}
   }
   else if(args[0]=="bounds") {
    byte[] data=Xml();BitConverter.GetBytes(uint.MaxValue).CopyTo(data,16);
    try{Call(r,"QuickSearchStringPool",new[]{typeof(byte[]),typeof(uint)},data,(uint)0);throw new Exception("Invalid count accepted");}catch(TargetInvocationException e){if(!(e.InnerException is InvalidDataException))throw;}
    data=Xml();try{Call(r,"QuickSearchStringPool",new[]{typeof(byte[]),typeof(uint)},data,(uint)3);throw new Exception("Invalid id accepted");}catch(TargetInvocationException e){if(!(e.InnerException is InvalidDataException))throw;}
   }
   else if(args[0]=="fixture" || args[0]=="sharing" || args[0]=="ownership") {
    bool valid=args[0]!="ownership"; string path=Fixture(valid); string moved=path+".moved";
    try {
     if(valid) using(var reader=new ApkReader(path)) {
      if(reader.PackageName!="example.fixture")throw new Exception("Direct manifest parsing failed");
      if(args[0]=="fixture") {using(var bmp=reader.getImage("icon.png",new Size(16,16)))if(bmp==null || bmp.Width!=16)throw new Exception("Entry zero icon failed");}
      else {using(var writer=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete)){writer.WriteByte(80);writer.Flush();} File.Move(path,moved);File.Move(moved,path);}
     } else { try{new ApkReader(path);throw new Exception("Missing manifest accepted");}catch(InvalidDataException){} using(var writer=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){} }
    } finally {File.Delete(path);File.Delete(moved);}
   }
   else if(args[0]=="boolean") { foreach(uint n in new uint[]{0,1,0xffffffff}) { string v=(string)Call(r,"convertData",new[]{typeof(byte[]),typeof(DATA_TYPE),typeof(uint)},new byte[0],DATA_TYPE.TYPE_INT_BOOLEAN,n); if(v!=(n==0?"false":"true")) throw new Exception("Incorrect boolean "+n); } }
   else { byte[] data={3,0,8,0,16,0,0,0,255,127,8,0,0,0,0,0};
    try { if(args[0]=="xml") Call(r,"ExtractCompressedXml",new[]{typeof(byte[])},data); else if(args[0]=="manifest") Call(r,"QuickSearchCompressedXml",new[]{typeof(byte[]),typeof(string),typeof(string)},data,"manifest","package"); else Call(r,args[0]=="strings"?"QuickSearchStringPool":"QuickSearchCompressedXMlResMap",new[]{typeof(byte[]),typeof(uint)},data,(uint)0); throw new Exception("Malformed input accepted"); }
    catch(TargetInvocationException e) { if(!(e.InnerException is InvalidDataException) && !(e.InnerException.InnerException is InvalidDataException)) throw; }
   }
   Console.WriteLine("PASS "+args[0]); return 0;
  } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
 }
}

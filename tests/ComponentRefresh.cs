using System;
using System.Drawing;
using System.IO; using System.IO.Compression;
using System.Xml;
using ApkShellext2;
using ApkQuickReader;
class ComponentRefresh {
 static int Main() {
  var temp=Path.Combine(Path.GetTempPath(),"ApkComponent-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
  try {
   var apk=Path.Combine(temp,"fixture.apk"); using(var file=File.Create(apk)) using(var archive=new ZipArchive(file,ZipArchiveMode.Create)) using(var entry=archive.CreateEntry("androidmanifest.xml").Open()) { entry.Write(new byte[]{0x03,0x00,0x08,0x00,0x00,0x00,0x00,0x00}); }
   using(var reader=AppPackageReader.Read(apk)) { if(reader == null) throw new Exception("Zip reader did not open empty archive"); }
   var xml=new XmlDocument(); xml.LoadXml("<vector xmlns:android='http://schemas.android.com/apk/res/android' android:width='24dp' android:height='24dp' android:viewportWidth='24' android:viewportHeight='24'><path android:fillColor='#FFFF0000' android:pathData='M0,0 L24,0 L24,24 L0,24 Z'/></vector>");
   using(var bitmap=new VectorDrawableRender(xml){size=new Size(64,64)}.image) { if(bitmap.GetPixel(32,32).A < 240 || bitmap.GetPixel(32,32).R < 200) throw new Exception("Skia vector output was not opaque red"); }
   Console.WriteLine("PASS System.IO.Compression facade and Svg.Skia vector rendering"); return 0;
  } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; } finally { try { Directory.Delete(temp,true); } catch {} }
 }
}

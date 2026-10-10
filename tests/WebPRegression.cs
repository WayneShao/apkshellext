using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using WebPWrapper;
class WebPRegression {
 [DllImport("libwebp_x64.dll",EntryPoint="WebPGetDecoderVersion",CallingConvention=CallingConvention.Cdecl)] static extern int Version64();
 [DllImport("libwebp_x86.dll",EntryPoint="WebPGetDecoderVersion",CallingConvention=CallingConvention.Cdecl)] static extern int Version32();
 static Bitmap Decode(byte[] data) {
  try {return (Bitmap)typeof(ApkQuickReader.ApkReader).Assembly.GetType("ApkShellext2.WebPImage",true).GetMethod("Decode",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{data});}
  catch(TargetInvocationException e){throw e.InnerException;}
 }
 static void Reject(byte[] data) {try{using(var bmp=Decode(data)){}throw new Exception("Malformed WebP accepted");}catch(InvalidDataException){}}
 static int Main(string[] args) {
  try {
   int version=Environment.Is64BitProcess?Version64():Version32();if(version!=0x010600)throw new Exception("Unexpected native decoder version "+version.ToString("X6"));
   byte[] data=File.ReadAllBytes(args[0]);Bitmap image;
   if(args.Length>1 && args[1]=="baseline") {using(var decoder=new WebP())image=decoder.Decode(data);}else image=Decode(data);
   using(image) {
    if(image.Width!=3 || image.Height!=2)throw new Exception("Incorrect dimensions");
    int[] alphas={255,128,0,255,64,192};int i=0;for(int y=0;y<2;y++)for(int x=0;x<3;x++){Color pixel=image.GetPixel(x,y);if(pixel.A!=alphas[i++])throw new Exception("Alpha lost at "+x+","+y+": "+pixel.A);}
    if(image.GetPixel(0,0).R!=255 || image.GetPixel(1,0).G!=255)throw new Exception("Incorrect color order");
   }
   Reject(new byte[0]);Reject(new byte[]{82,73,70,70});byte[] corrupt=(byte[])data.Clone();Array.Resize(ref corrupt,corrupt.Length/2);Reject(corrupt);byte[] badPayload=(byte[])data.Clone();for(int i=25;i<badPayload.Length;i++)badPayload[i]=255;Reject(badPayload);
   byte[] oversized=(byte[])data.Clone();int at=-1;for(int i=12;i+9<oversized.Length;i++)if(oversized[i]==86&&oversized[i+1]==80&&oversized[i+2]==56&&oversized[i+3]==76){at=i+9;break;}if(at<0)throw new Exception("Expected generated VP8L fixture");
   uint dims=BitConverter.ToUInt32(oversized,at);BitConverter.GetBytes((dims&0xf0000000u)|0x0fffffffu).CopyTo(oversized,at);Reject(oversized);
   for(int i=0;i<50;i++){Reject(badPayload);using(var next=Decode(data))if(next.GetPixel(1,0).A!=128)throw new Exception("Decode after failure failed");}
   Console.WriteLine("PASS WebP 1.6.0 RGBA, malformed/oversized input, repeated failure/success ("+(Environment.Is64BitProcess?"x64":"x86")+")");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}

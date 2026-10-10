using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.IO;
using System.Drawing;

namespace ApkShellext2 {
    /// <summary>
    /// Read AppxBundle
    /// </summary>
    public class AppxBundleReader :AppPackageReader {
        private ZipFile zip;
        private AppxReader appxReader= null;

        private const string AppxBundleManifestXml = @"AppxMetadata/AppxBundleManifest.xml";
        private const string ElemIdentity = @"Identity";
        //private const string ElemProperties = @"Properties";
        //private const string ElemDisplayName = @"DisplayName";
        private const string ElemPackage = @"Package";
        //private const string AttrVersion = @"Version";
        private const string AttrName = @"Name";
        //private const string AttrPublisher = @"Publisher";
        private const string AttrType = @"Type";
        private const string ValApplication = @"application";
        private const string AttrFileName = @"FileName";

        public AppxBundleReader(Stream stream) {
            FileName = "";
            openFile(stream);
        }

        public AppxBundleReader(string path) {
            FileName = path;
            openFile(PackageArchive.OpenFile(path));
        }

        private void openFile(Stream stream) {
            zip = PackageArchive.Open(stream, archive => {
                zip = archive;
                string appxFileName = null;
                var manifest = PackageArchive.ReadEntry(zip, AppxBundleManifestXml, PackageArchive.MetadataLimit);
                using (var input = new MemoryStream(manifest))
                using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) {
                    while (reader.Read()) {
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == ElemPackage && reader.GetAttribute(AttrType) == ValApplication) {
                            appxFileName = reader.GetAttribute(AttrFileName);
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(appxFileName))
                    throw new InvalidDataException("Cannot find application filename in " + AppxBundleManifestXml);
                // A deflated entry stream is not seekable. ZipFile requires seeking.
                var package = PackageArchive.ReadEntry(zip, appxFileName, PackageArchive.NestedPackageLimit);
                appxReader = new AppxReader(new MemoryStream(package, false));
            });
        }

        public override AppPackageReader.AppType Type {
            get {
                return AppType.WindowsPhoneAppBundle;
            }
        }

        public override Bitmap Icon {
            get {
                return appxReader.Icon;
            }
        }

        public override string AppName {
            get {
                return appxReader.AppName;
            }
        }

        public override string PackageName {
            get {
                return appxReader.PackageName;
            }
        }

        public override string Version {
            get {
                return appxReader.Version;
            }
        }

        public override string Publisher {
            get {
                return appxReader.Publisher;
            }
        }

        public override string AppID {
            get {
                return appxReader.AppID;
            }
        }
        private bool disposed = false;
        protected override void Dispose(bool disposing) {
            if (disposed) return;
            if (disposing) {
                if (appxReader != null) {
                    appxReader.Close();
                }
                if (zip != null)
                    zip.Close();
            }
            disposed = true;
            base.Dispose(disposing);
        }

        public void Close() {
            Dispose(true);
        }

        ~AppxBundleReader() {
            Dispose(true);
        }
    }
}

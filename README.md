# ApkShellext2![logo](https://github.com/kkguo/apkshellext/blob/ApkShellext2/ApkShellext2/Resources/Apkshellext_icons/apkshell_b64.png?raw=true)  
[![Crowdin](https://d322cqt584bo4o.cloudfront.net/apkshellext/localized.svg)](http://translate.apkshellext.com/)

A Windows shell extension supporting icon for files of
* .apk (android package)
* .ipa (iOS app package)
* .appx .appxbundle (Windows phone 8.1/10 app package, .xap is not supported)

This is the code repository, please visit the project page http://apkshellext.com or go to [release](https://github.com/kkguo/apkshellext/releases) for download.

#### Help this project
 * Become a Sponsor, I will be more glad to update your request.
 * Translators please read this first : [Translate Wiki page](https://github.com/kkguo/apkshellext/wiki/Translation-and-Multi-language-support#1-translate) 
 * Project pages needs web people to maintain.
 * Become a beta testers, join [![telegram](https://github.com/kkguo/apkshellext/blob/ApkShellext2/ApkShellext2/Resources/telegram_s.png)Telegram group](https://t.me/ApkShellExt2)
 * Pull request is always welcome.

#### Have issue?
 * Check the [Known issue list](https://github.com/kkguo/apkshellext/wiki/Known-issues-and-solution) first.
 * Look for help on [![telegram](https://github.com/kkguo/apkshellext/blob/ApkShellext2/ApkShellext2/Resources/telegram_s.png)Telegram group](https://t.me/ApkShellExt2) (Preferred)
  * Go raise [Issues](https://github.com/kkguo/apkshellext/issues) (Preferred)
  * Send to :email: issue@apkshellext.com

#### [Features]
 - [x] Display app icon in Windows File Explorer with best resolution. __DOES NOT work with other file manager due to .NET restriction__
 - [x] Customize-able Info-Tip for showing package information.
 - [x] Context menu for batch renaming, use customize-able patterns.
 - [x] Go to app store from context menu.
 - [x] Auto-check new version.
 - [x] Show overlay icon for different type of apps.
 - [x] Support multiple languages: Thanks to contributors on https://crowdin.com/project/apkshellext
    
#### [Todo]
 - [X] Adaptive-icon support (Beta)
 - [ ] protobuf support
 - [ ] support .NET 4.0??
 - [ ] QR code to download to phone
 - [ ] Hook up adb function with namespace extension.
 - [ ] drag-drop to install / uninstall to phone

#### Check [Wiki](https://github.com/kkguo/apkshellext/wiki) for how to build from source code

#### Credit :
|||
| --- | --- |
| [SharpShell](https://github.com/dwmkerr/sharpshell)                 | Shell extension library                        |
| [SharpZip](https://github.com/icsharpcode/SharpZipLib)              | Zip function implementation in C#              |
| [Iteedee.ApkReader](https://github.com/hylander0/Iteedee.ApkReader) | the original APK reader, not in use currently  |
| [PlistCS](https://github.com/animetrics/PlistCS)                    | iOS plist file reader                          |
| [PNGDecrush](https://github.com/MikeWeller/PNGDecrush)              | PNG decrush lib                                |
| [Ionic.Zlib](https://github.com/jstedfast/Ionic.Zlib)               | Another Zip implementation, used by PNGDecrush |
| [QRCoder](https://github.com/codebude/QRCoder)                      | QR code generator                              |
| [Ultimate Social](https://www.iconfinder.com/iconsets/ultimate-social) | A free icon set for social medias           |
| [SVG](https://github.com/vvvv/SVG)                                  | SVG format renderer                            |
| [WebP-Wrapper](https://github.com/JosePineiro/WebP-wrapper)         | WebP format renderer
| [Thanasis Georgiou](https://github.com/sakisds)                     | Project web page |
--------------
Originally this project hosted on [GoogleCode](code.google.com/p/apkshellext), now moved to [:octocat:Github](https://github.com/kkguo/apkshellext) and fully re-writen with a native apk reader. The active developing code is on [ApkShellext2 branch](https://github.com/kkguo/apkshellext/tree/ApkShellext2). The obsolete code is on [master branch](https://github.com/kkguo/apkshellext/tree/master)

## WayneShao maintenance fork

Version 0.2.0.8 focuses on download-safe shared reads, deterministic cleanup and malformed-package resilience. See [release notes](docs/release-0.2.0.8.md), [audit plan](docs/reliability-plan.md), and `tools/` for reproducible Windows builds/tests. Upstream COM identities are retained. Do not install over an already loaded old assembly: unregister and preserve a rollback copy first.

Upgrade note: if existing files still show old corner badges after replacing the DLL, run `tools/refresh-thumbnails.ps1 -Path <folder>` to re-extract that folder's supported package thumbnails without changing the files. A packaged copy is attached separately to the release.

## v0.3: .NET 10 LTS

The maintained shell extension now targets `net10.0-windows` and uses a native `.comhost.dll`, with exactly four existing shell CLSIDs. Install the x64 .NET 10 Desktop Runtime and deploy the complete release folder. Unregister the old .NET Framework DLL before registering the new host. See [v0.3 migration and installation notes](docs/release-0.3.0.md).

Build with `tools/build.ps1`; tests verify both target framework metadata and the actual .NET10 runtime. `tools/test-native-com.ps1` exercises native COM callbacks without registering the candidate; its `-Registered` mode checks the installed host. The manual GUI and optional service also build on.NET10, but the service is not installed by this release.

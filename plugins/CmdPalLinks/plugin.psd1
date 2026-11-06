@{
    # 插件元数据：shared/build/*.ps1 只从这里取插件相关常量，脚本本身不含插件名。
    Name              = 'CmdPalLinks'
    DisplayName       = '链接库'
    Description       = '本地链接 / 文件 / 文件夹收藏与检索'
    Manifest          = 'src/CmdPalLinks/Package.appxmanifest'
    Project           = 'src/CmdPalLinks/CmdPalLinks.csproj'
    PackageFamilyName = 'CmdPalLinks_byt16r9dq2e2y'
    CertSubject       = 'CN=EverythingCmdPal Dev'
    ProcessName       = 'CmdPalLinks'
    HostProcessName   = 'Microsoft.CmdPal.UI'
    HostMinimumVersion = '0.100.2'
    TagPrefix         = 'cmdpallinks-v'
}

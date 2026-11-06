@{
    # 插件元数据：shared/build/*.ps1 只从这里取插件相关常量，脚本本身不含插件名。
    Name              = 'EverythingCmdPal'
    DisplayName       = 'Everything for Command Palette'
    Description       = 'PowerToys Command Palette 的 Everything 搜索扩展'
    Manifest          = 'src/EverythingCmdPal/Package.appxmanifest'
    Project           = 'src/EverythingCmdPal/EverythingCmdPal.csproj'
    PackageFamilyName = 'EverythingCmdPal_byt16r9dq2e2y'
    CertSubject       = 'CN=EverythingCmdPal Dev'
    ProcessName       = 'EverythingCmdPal'
    HostProcessName   = 'Microsoft.CmdPal.UI'
    HostMinimumVersion = '0.100.2'
    TagPrefix         = 'everythingcmdpal-v'
}

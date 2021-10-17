# SyncTools.Net
Update Windows SysInternal tools

### Description
This tool is heavily drawn from [Kenny Kerr](https://github.com/kennykerr)'s SyncTools for (Windows Sysinternals)[https://docs.microsoft.com/en-us/sysinternals/].

It uses Windows Sysinternals [live url](https://live.sysinternals.com/) to check and download the tools.

Since I was introduced to Sysinternals (formerly known as Winternals), I have relied on it several times and have found myself returning to them time and again. I had found Kenny's synctool to help me keep the tools up to date. However, lately this tiny yet very helpful tool went missing from the face of the earth. And nowadays without a reliable and trustworthy similar tool, I decided to create my own.

I did create one, but did not have a confirmed strategy, it would download all the tools and overwrite them. After I stumbled across Kenny's [gist](https://gist.github.com/kennykerr/d72b59a7674001f51431cb973df84cdd), I decided to port it to C#.

#### TODO list
- Print new version with file
- Figure out how to print download progress in console
- Cleaner code structure
- Asynchronous file download, possibly multiple simultaneously

#### Known bugs & issues
- Download status is not updated correctly. Need to consolidated list of up to date and updated tools.
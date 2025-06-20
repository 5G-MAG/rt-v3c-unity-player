<h1 align="center">Frequently Asked Questions</h1>

## What  are the V3C Immersive Platform supported features ?
Please, read the description on the 5G-MAG reference Tool webpage [Getting Started/V3C Immersive Platform](https://5g-mag.github.io/Getting-Started/pages/v3c-immersive-platform/) to get an overview of the features of the platform and the 
[release note](../release_note.md) to get informations on the features currently supported.

## What  are the supported devices ?
What is compatible is a smartphone/tablet with OpenGL ES v320, equipped with a chipset Snapdragon (GPU Adreno).
For example, OnePLus10T or Samsung Galaxy tabS9 are supported devices.

## Questions on the setup to build the projects
### Do I need LFS to clone the projects ?
yes, to clone the project you need to setup LFS to your git configuration.

### How can I solve link error when I build the project v3c-decoder-plugin ?
The error *LNK2019: unresolved external symbol __std_minmax_element_f* is solved by installing Visual Studio Professional 2022 17.10.2 (or higher)

### A (partial) rebuild of the V3CImmersiveTest Unity project fails with a CMake configuration error

Remove the `.utmp` directory and try again.

## Questions on usage of the platform
### Why my Local content is not played in the SimplePlayer Application ?
There may be several reasons for this.  
Indications may be given by log files accessible in your user folder:  
**Windows**  
C:/Users/[your-user-name]/AppData/LocalLow/InterDigital/V3CSimplePlayer/  
**Android**  
file/storage/emulated/0/Android/data/com.InterDigital.V3CSimplePlayer/files  
or Android/data/com.InterDigital.V3CSimplePlayer/files  

- Some DLL may be missing
1. Check that you apply carefully the section **Decoder plugin installation** of the [readme](../README.md)
2. Check the log file **player.log** accessible in your user folder.  
If you have this kind of message:  
*Fallback handler could not load library C:/V3CImmersiveTest/v3c-unity-player/V3CImmersiveTest/exe/V3CSimplePlayer_Data/MonoBleedingEdge/V3CImmersiveDecoderVideo**  
it means you probably forgot to copy the DLL from the decoder-plugin repository to the unity-player repository.
Have a look to the readme file that indicates how to use the **copy_dll.sh** script


- Data set-up
1. Check that you apply carefully the section **Data Setup** of the [readme](../README.md).
2. Check the log file called **V3CImmersiveDecoderVideo.log**  
You should ignored errors related to haptics, as haptics is not yet activated in this release.  
For local data, you may see error messages related to content not found.  In this case, please check carefully you have copied content in the right place of your user folder indicated above.  

### Why my Remote content is not played in the SimplePlayer Application ?
There also may be several reasons for this. Indications may be given by log files accessible in your user folder as described above.
- In **V3CImmersiveDecoderVideo.log** you have the error **[ERROR] (...) ClientInterface cannot initialize streamer**  
This message is related to content not found on the DASH server, or indicating that the DASH server is not started or not available.
This may be due to DASH Server configuration issue. Please have a look to the v3c-unity-player **README**, **README-test-config.md** located in [v3c-decoder-plugin/Tests](https://github.com/5G-MAG/rt-v3c-decoder-plugin), and **README_dash_server.md** located in [v3c-decoder-plugin/Tests/remote-data](https://github.com/5G-MAG/rt-v3c-decoder-plugin)  
 
- In **V3CImmersiveDecoderVideo.log** you have the error **[ERROR] (...) Module not found: v3c_dash_streamer.dll**  
It means the Dash Streamer DLL is not load on Unity. You need to activate the v3c DASH Streamer DLL on Unity.   
As indicated in the [v3c-unity-player readme](../README.md), you need to check the box "Load on startup" for the file v3c_dash_streamer of the package "V3C Decoder/Runtime/Plugins" and select "Apply".  

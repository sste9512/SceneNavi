using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using SceneNavi.ROMHandler;

namespace SceneNavi.HeaderCommands
{
    public class SettingsSoundScene : Generic, IStoreable
    {
        // https://www.the-gcn.com/topic/2471-the-beginners-guide-to-music-antiqua-teasers/?p=40641

        //Just to complement this info, the yy above referred as "music playback" option stands for the scene's night bgm.
        //The night bgm uses a different audio type, that plays nature sounds.
        //A setting of 0x00 is found into scenes with the complete day-night cycle and will play the standard night noises.
        //The 0x13 setting is found in dungeons and indoors, so the music will be always playing, independent of the time of the day.
        //01 - Standard night [Kakariko]
        //02 - Distant storm [Graveyard]
        //03 - Howling wind and cawing [Ganon's Castle]
        //04 - Wind + night birds [Kokiri]
        //05, 08, 09, 0D, 0E, 10, 12 - Wind + crickets
        //06,0C - Wind
        //07 - Howling wind
        //0A - Tubed howling wind [Wasteland]
        //0B - Tubed howling wind [Colossus]
        //0F - Wind + birds
        //14, 16, 18, 19, 1B, 1E - silence
        //1C - Rain
        //17, 1A, 1D, 1F - high tubed wind + rain

        public byte Reverb { get; set; }
        public byte NightSfxId { get; set; }
        private byte TrackId { get; set; }

        public SettingsSoundScene(Generic baseCommand)
            : base(baseCommand)
        {
            Reverb = (byte) ((Data >> 48) & 0xFF);
            NightSfxId = (byte) ((Data >> 8) & 0xFF);
            TrackId = (byte) (Data & 0xFF);
        }

        public void Store(byte[] dataBuffer, int baseAddress)
        {
            dataBuffer[baseAddress + (Offset & 0xFFFFFF) + 1] = Reverb;
            dataBuffer[baseAddress + (Offset & 0xFFFFFF) + 6] = NightSfxId;
            dataBuffer[baseAddress + (Offset & 0xFFFFFF) + 7] = TrackId;
        }
    }
}
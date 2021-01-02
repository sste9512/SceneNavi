using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using SceneNavi.RomHandlers;

namespace SceneNavi.ROMHandler
{
    public class EntranceTableEntry
    {
        private byte[] CodeData { get; set; }
        private byte[] Data { get; set; }
        
        [ReadOnly(true)]
        public ushort Number { get; set; }

      
        [Browsable(false)] private int Offset { get; set; }

        [Browsable(false)] private bool IsOffsetRelative { get; set; }
        
       
        [DisplayName("Scene #"), Description("Scene number to load")] 
        private byte SceneNumber { get; set; }
       
        
        [DisplayName("Entrance #"), Description("Entrance within scene to spawn at")]
        private byte EntranceNumber { get; set; }


        [DisplayName("Variable"), Description("Controls certain behaviors when transitioning, ex. stopping music")] 
        private byte Variable { get; set; }
      
        
        [DisplayName("Fade"), Description("Animation used when transitioning")] 
        private byte Fade { get; set; }

        
        
        [DisplayName("Scene Name")]
        public string SceneName
        {
            get => (SceneNumber < _baseRom.Rom.Scenes.Count ? _baseRom.Rom.Scenes[SceneNumber].GetName() : "(invalid?)");

            set
            {
                var scnidx = _baseRom.Rom.Scenes.FindIndex(x => string.Equals(x.GetName(), value, StringComparison.InvariantCultureIgnoreCase));
                if (scnidx != -1)
                    SceneNumber = (byte)scnidx;
                else
                    System.Media.SystemSounds.Hand.Play(); // wtf, why is this in the entrance table entry data entity? 
            }
        }

        // remove this from class
        readonly BaseRomHandler _baseRom;
        
        
        
        // This constructor will be obsolete
        public EntranceTableEntry(BaseRomHandler baseRom, int ofs, bool isRelativeOffset)
        {
            _baseRom = baseRom;
            Offset = ofs;
            IsOffsetRelative = isRelativeOffset;

            SceneNumber = (IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data)[ofs];
            EntranceNumber = (IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data)[ofs + 1];
            Variable = (IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data)[ofs + 2];
            Fade = (IsOffsetRelative ? baseRom.Rom.CodeData : baseRom.Rom.Data)[ofs + 3];
        }
        
        
        // This is the new real constructor
        public EntranceTableEntry(byte[] codeData, byte[] data, int offset, bool isOffsetRelative)
        {
            CodeData = codeData;
            Data = data;
            Offset = offset;
            IsOffsetRelative = isOffsetRelative;

            SceneNumber = (IsOffsetRelative ? CodeData : Data)[offset];
            EntranceNumber = (IsOffsetRelative ? CodeData : Data)[offset + 1];
            Variable = (IsOffsetRelative ? CodeData : Data)[offset + 2];
            Fade = (IsOffsetRelative ? CodeData : Data)[offset + 3];
        }

        public void SaveTableEntry()
        {
            (IsOffsetRelative ? CodeData : Data)[Offset] = SceneNumber;
            (IsOffsetRelative ? CodeData : Data)[Offset + 1] = EntranceNumber;
            (IsOffsetRelative ? CodeData : Data)[Offset + 2] = Variable;
            (IsOffsetRelative ? CodeData : Data)[Offset + 3] = Fade;
        }
    }
}

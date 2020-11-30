using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SceneNavi.SimpleF3DEX2
{
    public class OtherModeL
    {
        public static OtherModeL Empty = new OtherModeL(0);

        public uint Data { get; private set; }

        public bool AAEn => (Data & (uint)General.OtherModeL.AA_EN) != 0;
        public bool ZCmp => (Data & (uint)General.OtherModeL.Z_CMP) != 0;
        public bool ZUpd => (Data & (uint)General.OtherModeL.Z_UPD) != 0;
        public bool ImRd => (Data & (uint)General.OtherModeL.IM_RD) != 0;
        public bool ClrOnCvg => (Data & (uint)General.OtherModeL.CLR_ON_CVG) != 0;
        public bool CvgDstWrap => (Data & (uint)General.OtherModeL.CVG_DST_WRAP) != 0;
        public bool CvgDstFull => (Data & (uint)General.OtherModeL.CVG_DST_FULL) != 0;
        public bool CvgDstSave => (Data & (uint)General.OtherModeL.CVG_DST_SAVE) != 0;
        public bool ZModeInter => (Data & (uint)General.OtherModeL.ZMODE_INTER) != 0;
        public bool ZModeXlu => (Data & (uint)General.OtherModeL.ZMODE_XLU) != 0;
        public bool ZModeDec => (Data & (uint)General.OtherModeL.ZMODE_DEC) != 0;
        public bool CvgXAlpha => (Data & (uint)General.OtherModeL.CVG_X_ALPHA) != 0;
        public bool AlphaCvgSel => (Data & (uint)General.OtherModeL.ALPHA_CVG_SEL) != 0;
        public bool ForceBl => (Data & (uint)General.OtherModeL.FORCE_BL) != 0;

        public OtherModeL(uint data)
        {
            Data = data;
        }
    }
}

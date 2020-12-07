namespace SceneNavi.ROMHandler.Interfaces
{
    public interface IRomReader
    {
        void ReadRomHeader();
        void ReadEntranceTable();
        void ReadDmaTable();
        void ReadFileNameTable();
    }

    public interface IRomResolver 
    {
        void FindBuildInfo();
        void FindFileNameTable();
        void FindCodeFile();
        void FindSceneTable();
        void FindActorTable();
        void FindObjectTable();
    }

    public interface IRomHandler : IRomReader, IRomResolver
    {
        bool IsAddressSupported(uint address);
        void DetectByteOrder();
    }
}
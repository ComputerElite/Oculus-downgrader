using System.IO;

namespace QuestPatcher.Axml
{
    /// <summary>
    /// Class with read/write methods convenient for AXML handling
    /// </summary>
    internal static class BinaryStreamExtensions
    {
        internal static ResourceType ReadResourceType(this BinaryReader reader)
        {
            int t = reader.ReadInt32();
            return (ResourceType) (t & 0xFFFF);
        }

        internal static void WriteChunkHeader(this BinaryWriter writer, ResourceType typeEnum, int length = 0)
        {
            length += 8; // Length should include type and itself (two integers, so 8 extra bytes)

            int typePrefix = 0;
            switch(typeEnum)
            {
                case ResourceType.Xml:
                    typePrefix = 0x0008;
                    break;
                case ResourceType.XmlResourceMap:
                    typePrefix = 0x0008;
                    break;
                case ResourceType.StringPool:
                    typePrefix = 0x001C;
                    break;
                default:
                    typePrefix = 0x0010;
                    break;
            }
            writer.Write((int) typeEnum | typePrefix << 16);
            writer.Write(length);
        }
    }
}
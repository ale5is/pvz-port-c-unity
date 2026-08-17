using System;

public sealed class PvZPakFile
{
    public string Name { get; }
    public long Offset { get; }
    public int Size { get; }
    public long Timestamp { get; }

    public PvZPakFile(string name, long offset, int size, long timestamp)
    {
        Name = name;
        Offset = offset;
        Size = size;
        Timestamp = timestamp;
    }

    public override string ToString()
    {
        return $"{Name} | Offset={Offset} | Size={Size}";
    }
}
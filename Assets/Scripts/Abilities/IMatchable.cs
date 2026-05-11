public interface IMatchable
{
    bool IsMatched { get; }
    void MarkAsMatched();
}
namespace DockerGC
{
    using System.Collections.Generic;

    public interface IMatchlist
    {
        bool Match(string input, bool ignoreCase = true);

        bool MatchAny(IList<string> inputs, bool ignoreCase = true);

        IList<string> ToList();
    }
}
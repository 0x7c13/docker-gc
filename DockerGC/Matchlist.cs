namespace DockerGC
{
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class Matchlist : IMatchlist
    {
        private List<string> _whitelist = new List<string>();

        public Matchlist() { }

        public Matchlist(string input) 
        {
            if (String.IsNullOrWhiteSpace(input)) return;

            foreach (var entry in input.Split(",", StringSplitOptions.RemoveEmptyEntries)) 
            {
                if (!String.IsNullOrWhiteSpace(entry)) 
                {
                    _whitelist.Add(entry.Trim());
                }
            }
        }
        
        public static Matchlist Parse(string input) 
        {
            return new Matchlist(input);
        }

        private bool _caseInsensitiveContains(string str, string substr, StringComparison stringComparison)
        {
            return str.IndexOf(substr, stringComparison) >= 0;
        }

        public bool Match(string input, bool ignoreCase = true) 
        {
            if (String.IsNullOrWhiteSpace(input)) return false;
            
            var matchResult = false;

            foreach (var entry in _whitelist) 
            {
                if (entry.StartsWith("*") && entry.EndsWith("*")) 
                {
                    matchResult = _caseInsensitiveContains(input, entry.Substring(1, entry.Length - 2), ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture);
                }
                else if (entry.StartsWith("*")) 
                {
                    matchResult = input.EndsWith(entry.Substring(1), ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture);
                } 
                else if (entry.EndsWith("*")) 
                {
                    matchResult = input.StartsWith(entry.Substring(0, entry.Length - 1), ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture);
                }
                else 
                {
                    matchResult = (String.Compare(input, entry, ignoreCase) == 0);
                }

                if (matchResult) break;
            }

            return matchResult;
        }

        public bool MatchAny(IList<string> inputs, bool ignoreCase = true) 
        {
            if (inputs == null) return false;

            foreach (var input in inputs) 
            {
                if (Match(input, ignoreCase)) return true;
            }
            return false;
        }

        public IList<string> ToList() 
        {
            return _whitelist;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironmelt
{
    public class IronmeltError : IComparable<IronmeltError>
    {
        public string Error { get; set; }
        public int Count { get; set; }
        public string Details { get; set; }
        public Severity Severity { get; set; }

        public IronmeltError(string error, int count, string details, Severity severity)
        {
            Error = error;
            Count = count;
            Details = details;
            Severity = severity;
        }

        public int CompareTo(IronmeltError other)
        {
            return Count.CompareTo(other.Count);
        }
    }

    public enum Severity
    {
        Unknown, Low, Medium, High
    }
}

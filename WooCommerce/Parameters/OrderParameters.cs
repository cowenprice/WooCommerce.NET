using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WooCommerce.NET.WooCommerce.Parameters
{
    public class OrderParameters : WCItemParameters
    {
        public string? Context { get; set; }
        public int? Page { get; set; } = 1;
        public int? PerPage { get; set; } = 10;
        public string? Search { get; set; }
        public string? After { get; set; }
        public string? Before { get; set; }
        public string? ModifiedAfter { get; set; }
        public string? ModifiedBefore { get; set; }
        public bool? DatesAreGmt { get; set; }
        public IList<int>? Exclude { get; set; }
        public IList<int>? Include { get; set; }
        public int? Offset { get; set; }
        public string? Order { get; set; }
        public string? OrderBy { get; set; }
        public IList<int>? Parent { get; set; }
        public IList<int>? ParentExclude { get; set; }
        public IList<string>? Status { get; set; }
        public int? Customer { get; set; }
        public int? Product { get; set; }
        public int? DecimalPoints { get; set; }
    }
}

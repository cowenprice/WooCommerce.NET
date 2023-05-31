using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WooCommerce.NET.WooCommerce.Parameters
{
    public interface IWCItemParameters
    {
        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

namespace ServerTextProcessor1
{
    class FrequencyContext : DbContext
    {
        public FrequencyContext()
            : base("DbConnection")
        { }

        public DbSet<Frequency> Frequencies { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Torque
{
    class TorqueService : ITorqueService
    {

        public TorqueService(TorqueServiceOptions options)
        {
        }

        public void Dispose()
        {
        }

        public async Task<float> ReadAsync()
        {
            return 0f;
        }
    }
}

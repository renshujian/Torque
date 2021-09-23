using System;
using System.Threading.Tasks;

namespace Torque
{
    public interface ITorqueService
    {
        Task Zero();
        Task<double> ReadAsync();
        void StopRead();
    }
}

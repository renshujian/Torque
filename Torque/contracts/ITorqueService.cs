using System;
using System.Threading.Tasks;

namespace Torque
{
    public interface ITorqueService
    {
        Task Zero();
        Task<float> ReadAsync();
        void StopRead();
    }
}

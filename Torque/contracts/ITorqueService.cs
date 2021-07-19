using System;
using System.Threading.Tasks;

namespace Torque
{
    public interface ITorqueService : IDisposable
    {
        Task<float> ReadAsync();
    }
}

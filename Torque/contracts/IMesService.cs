using System;
using System.Collections.Generic;

namespace Torque
{
    public interface IMesService : IDisposable
    {
        Tool? GetTool(string id);
        void Upload(IList<Test> result);
    }
}

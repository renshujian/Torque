using System.Collections.Generic;
using System.Linq;

namespace Torque
{
    class MesService : IMesService
    {
        readonly MesDbContext db;

        public MesService(MesDbContext mesDbContext)
        {
            db = mesDbContext;
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public Tool GetTool(string id)
        {
            return db.Tools.Where(t => t.Id == id).First();
        }

        public void Upload(IList<Test> result)
        {
            db.Tests.AddRange(result);
            db.SaveChanges();
        }
    }
}

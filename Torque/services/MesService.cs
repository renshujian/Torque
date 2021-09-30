using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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

        public Tool? GetTool(string id)
        {
            return db.Tools.FromSqlRaw("SELECT SCREWDRIVER, XYNJ FROM SCREWDRIVER_CMK WHERE SCREWDRIVER={0} AND CURRENTVALUE='是'", id).FirstOrDefault();
        }

        public void Upload(IList<Test> result)
        {
            db.Tests.AddRange(result);
            db.SaveChanges();
        }
    }
}

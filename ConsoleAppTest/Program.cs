using CemexDataAcces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // PruebaConeccin();

                //prueba.PruebaConexionSQL();
                TestConection();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }

        static void TestConection()
        {
            string cn = @"Server=tcp:pgrqasdbsirsi011.database.windows.net,1433;Initial Catalog=pgrqasqlsirsi013;Persist Security Info=False;User ID=sirsisqladmin;Password=#>5irP$O].";
            SQLDataAccess acces = new SQLDataAccess(cn,5,1,2,5);
            var res = acces.ExecuteReaderPolicy("INSERTs INTO TestAV.PingTable VALUES (1); DELETE TestAV.PingTable", null);

        }
    }
}

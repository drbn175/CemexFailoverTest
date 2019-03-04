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
                var prueba = new PruebaConexion();
                prueba.PruebaConexionSQL();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }

        static void PruebaConeccin()
        {
            string cnn = @"Server=tcp:pgrqasdbsirsi011.database.windows.net,1433;Initial Catalog=pgrqasqlsirsi013;Persist Security Info=False;User ID=sirsisqladmin;Password=#>5irP$O].";
            SQLDataAccess acces = new SQLDataAccess(cnn, TimeSpan.FromMinutes(5));
            var res = acces.EjecutaReaderPolicy("select *  from ApplicantAnnouncements", null);

        }
    }
}

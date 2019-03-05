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
                PruebaConeccion();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }

        static void PruebaConeccion()
        {
            string cn = @"Server=tcp:pgrqasdbsirsi011.database.windows.net,1433;Initial Catalog=pgrqasqlsirsi013;Persist Security Info=False;User ID=sirsisqladmin;Password=#>5irP$O].";
            SQLDataAccess acces = new SQLDataAccess(cn);
            var res = acces.EjecutaReaderPolicy("select top 5 *  from ApplicantAnnouncements", null);

        }
    }
}

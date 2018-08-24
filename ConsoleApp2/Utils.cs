using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    class Utils
    {

        public static DateTime toDateTime(string date)
        {
            return DateTime.ParseExact(date.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        public static DateTime toHourTime(string date)
        {
            return DateTime.ParseExact(date.Trim(), "H:mm:ss", CultureInfo.InvariantCulture);
        }

 
        public static DataRow toDataRow(DataTable dt, Prueba sqlRow)
        {
            DataRow newRow = dt.NewRow();
            newRow[0] = sqlRow.Fecha;
            newRow[1] = sqlRow.Hora;
            newRow[2] = sqlRow.Ubicación;
            newRow[3] = sqlRow.Operario;
            newRow[4] = sqlRow.Cargadora;
            newRow[5] = sqlRow.Producto;
            newRow[6] = sqlRow.Función;
            newRow[7] = sqlRow.Secuencia;
            newRow[8] = sqlRow.Peso;
            newRow[9] = sqlRow.Actividad;
            newRow[10] = sqlRow.Origen;
            newRow[11] = sqlRow.Camión;
            newRow[12] = sqlRow.Dirección;
            newRow[13] = sqlRow.Datos5;
            newRow[14] = sqlRow.Tara;
            newRow[15] = sqlRow.Notas;
            newRow[16] = sqlRow.Ciclo;
            newRow[17] = sqlRow.Rendimiento;
            newRow[18] = sqlRow.Mascara;
            newRow[19] = sqlRow.FechaMina;
            newRow[20] = sqlRow.Turno;
            newRow[21] = sqlRow.SecuenciaID;
            return newRow;
        }

        public static Registry toRegistry(Prueba sqlRow)
        {
            Registry newRegistry = new Registry();
           
            newRegistry.Fecha  = Utils.toDateTime(sqlRow.Fecha.Trim());
            newRegistry.Hora  = Utils.toHourTime(sqlRow.Hora.Trim());
            newRegistry.Ubicación  = sqlRow.Ubicación.Trim();
            newRegistry.Operario  = sqlRow.Operario.Trim();
            newRegistry.Cargadora  = sqlRow.Cargadora.Trim();
            newRegistry.Producto  = sqlRow.Producto.Trim();
            newRegistry.Función  = sqlRow.Función.Trim();
            newRegistry.Secuencia  = sqlRow.Secuencia.Trim();
            newRegistry.Peso = sqlRow.Peso.Trim()  == "" ? (Nullable<double>)null : double.Parse(sqlRow.Peso.Trim(), CultureInfo.GetCultureInfo("es-MX"));
            newRegistry.Actividad  = sqlRow.Actividad.Trim();
            newRegistry.Origen  = sqlRow.Origen.Trim();
            newRegistry.Camión  = sqlRow.Camión.Trim();
            newRegistry.Dirección  = sqlRow.Dirección.Trim();
            newRegistry.Datos5  = sqlRow.Datos5.Trim();
            newRegistry.Tara  = sqlRow.Tara.Trim();
            newRegistry.Notas  = sqlRow.Notas.Trim();
            newRegistry.Ciclo  = sqlRow.Ciclo.Trim() == "" ? (Nullable<double>)null : double.Parse(sqlRow.Ciclo.Trim());
            newRegistry.Rendimiento  = sqlRow.Rendimiento.Trim() == "" ? (Nullable<double>)null : double.Parse(sqlRow.Rendimiento.Trim());
            newRegistry.Mascara  = sqlRow.Mascara.Trim();
            newRegistry.FechaMina  = Utils.toDateTime(sqlRow.FechaMina.Trim());
            newRegistry.Turno  = sqlRow.Turno.Trim();
            newRegistry.SecuenciaID = Convert.ToInt64(sqlRow.SecuenciaID.Trim());
            return newRegistry;
        }

        public static Prueba toPrueba(Registry rRow)
        {
            Prueba newPrueba = new Prueba();
            
            newPrueba.Fecha = rRow.Fecha.ToString("dd/MM/yyyy").Trim();
            newPrueba.Hora = rRow.Hora.ToString("H:mm:ss").Trim();
            newPrueba.Ubicación = rRow.Ubicación == null ? "" : (string)rRow.Ubicación;
            newPrueba.Operario = rRow.Operario == null ? "" : (string)rRow.Operario;
            newPrueba.Cargadora = rRow.Cargadora == null ? "" : (string)rRow.Cargadora;
            newPrueba.Producto = rRow.Producto == null ? "" : (string)rRow.Producto;
            newPrueba.Función = rRow.Función == null ? "" : (string)rRow.Función;
            newPrueba.Secuencia = rRow.Secuencia == null ? "" : (string)rRow.Secuencia; // Should be nulleable int
            newPrueba.Peso = rRow.Peso.ToString();
            newPrueba.Actividad = rRow.Actividad == null ? "" : (string)rRow.Actividad;
            newPrueba.Origen = rRow.Origen == null ? "" : (string)rRow.Origen;
            newPrueba.Camión = rRow.Camión == null ? "" : (string)rRow.Camión;
            newPrueba.Dirección = rRow.Dirección == null ? "" : (string)rRow.Dirección;
            newPrueba.Datos5 = rRow.Datos5 == null ? "" : (string)rRow.Datos5;
            newPrueba.Tara = rRow.Tara == null ? "" : (string)rRow.Tara;
            newPrueba.Notas = rRow.Notas == null ? "" : (string)rRow.Notas;
            newPrueba.Ciclo = rRow.Ciclo.ToString();
            newPrueba.Rendimiento = rRow.Rendimiento.ToString();
            newPrueba.Mascara = rRow.Mascara == null ? "" : (string)rRow.Mascara;
            newPrueba.FechaMina = rRow.FechaMina.ToString("dd/MM/yyyy").Trim();
            newPrueba.Turno = rRow.Turno == null ? "" : (string)rRow.Turno;
            newPrueba.SecuenciaID = rRow.SecuenciaID.ToString();
            return newPrueba;
        }


        public static void printDataTable(DataTable dt)
        {
            foreach (DataRow dataRow in dt.Rows)
            {
                foreach (var item in dataRow.ItemArray)
                    Console.Write(item + ";");
                Console.WriteLine();
            }
        }

        public static void printDataTable2(DataTable dt)
        {
            foreach (DataRow row in dt.Rows)
            {
                Console.Write($"{row["Cargadora"]} | {row["Fecha"]} | {row["Hora"].ToString().PadLeft(8, '0')} | {prettyPrint(row["Ciclo"], 1)} | {prettyPrint(row["Rendimiento"], 60)} | {row["Mascara"].ToString()}");
                Console.WriteLine();
            }
        }

        private static string prettyPrint(object a, int b)
        {
            if (a.ToString() == "") return "";
            return String.Format("{0:0.000}", Convert.ToDouble(a.ToString()) / (60 / b));
        }

    }
}

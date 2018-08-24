using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    public class Registry
    {
        public DateTime Fecha { get; set; }
        public DateTime Hora { get; set; }
        public string Ubicación { get; set; }
        public string Operario { get; set; }
        public string Cargadora { get; set; }
        public string Producto { get; set; }
        public string Función { get; set; }
        public string Secuencia { get; set; }
        public double? Peso { get; set; }
        public string Actividad { get; set; }
        public string Origen { get; set; }
        public string Camión { get; set; }
        public string Dirección { get; set; }
        public string Datos5 { get; set; }
        public string Tara { get; set; }
        public string Notas { get; set; }
        public double? Ciclo { get; set; }
        public double? Rendimiento { get; set; }
        public string Mascara { get; set; }
        public DateTime FechaMina { get; set; }
        public string Turno { get; set; }
        public long SecuenciaID { get; set; }
        public long ID { get; set; }
    }
}

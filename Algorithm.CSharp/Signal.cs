using System;
 
 namespace QuantConnect.Algorithm.CSharp
 {
     internal class Signal
     {
         public DateTime Time { get; set; }
         public string Type { get; set; }

         public double GetTotalMinutes(DateTime time) { return (time - Time).TotalMinutes; }
     }
 }
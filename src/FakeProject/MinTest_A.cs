using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FakeProject
{
    //Single line comments get included too
    public class MinTest_A : IMinTestA
    {
        readonly MinTest_B _B = new();
        
        /// <summary>
        /// Do some important work
        /// </summary>
        public void DoSomething()
        {
            _B.DoThingB();
        }
    }
}

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace WwTool.Common
{
    public interface IConfigureService
    {
        Task ConfigureAsync();
    }
}

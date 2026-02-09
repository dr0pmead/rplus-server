using System.Threading.Tasks;

namespace RPlus.Gateway.Application.Interfaces.Services;

public interface ISystemModeProvider
{
    Task<string> GetCurrentModeAsync(); // Normal, Maintenance, Lockdown
}

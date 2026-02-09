using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RPlus.Gateway.Application.Interfaces.Services;

public interface IContextEnricher
{
    Task EnrichAsync(HttpContext context);
}

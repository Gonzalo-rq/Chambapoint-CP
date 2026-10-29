using System.ComponentModel;
using ChambaPoint.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace ChambaPoint.Api.Services.Agent;

public class TrabajadoresPlugin
{
    private readonly AppDbContext _db;

    public TrabajadoresPlugin(AppDbContext db)
    {
        _db = db;
    }

    [KernelFunction, Description("Busca trabajadores disponibles por su oficio o especialidad (electricista, gasfitero, pintor, carpintero)")]
    public async Task<string> BuscarTrabajadoresPorOficio(
        [Description("Nombre del oficio o profesión a buscar")] string oficio)
    {
        var rawWorkers = await _db.Workers
            .Include(w => w.User)
            .Include(w => w.Reviews)
            .Where(w => EF.Functions.Like(w.Profession, $"%{oficio}%"))
            .Take(3)
            .ToListAsync();

        if (!rawWorkers.Any())
        {
            return $"No se encontraron trabajadores disponibles para el oficio de '{oficio}'.";
        }

        var trabajadores = rawWorkers.Select(w => new
        {
            w.Id,
            Nombre = w.User != null ? w.User.Name : "Sin nombre",
            Oficio = w.Profession,
            Rating = w.Reviews.Count > 0 ? Math.Round(w.Reviews.Average(r => (double)r.Rating), 1) : 0,
            TrabajosCompletados = w.JobsCount,
            AñosExperiencia = w.ExperienceYears
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(trabajadores);
    }
}

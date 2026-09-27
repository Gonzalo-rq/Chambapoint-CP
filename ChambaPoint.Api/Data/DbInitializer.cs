using ChambaPoint.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Data;

public static class DbInitializer
{
    public static void Seed(AppDbContext db, IPasswordHasher<User> hasher)
    {
        var demoSeedData = new[]
        {
            new
            {
                Name = "Ana Cliente",
                Email = "ana.cli@example.com",
                Role = Roles.Customer,
                AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150",
                IsWorker = false,
                Profession = "",
                Experience = 0,
                Distance = 0.0,
                Jobs = 0,
                About = "",
                Lat = (double?)null,
                Lng = (double?)null
            },
            new
            {
                Name = "Carlos Electricista",
                Email = "carlos.elec2@example.com",
                Role = Roles.Worker,
                AvatarUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150",
                IsWorker = true,
                Profession = RequestCategories.Electricidad,
                Experience = 8,
                Distance = 2.1,
                Jobs = 45,
                About = "Especialista en instalaciones eléctricas residenciales y comerciales.",
                Lat = (double?)-12.1187,
                Lng = (double?)-77.0330
            },
            new
            {
                Name = "María Gasfitera",
                Email = "maria.gas@example.com",
                Role = Roles.Worker,
                AvatarUrl = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=150",
                IsWorker = true,
                Profession = RequestCategories.Plomeria,
                Experience = 6,
                Distance = 1.4,
                Jobs = 38,
                About = "Instalación y reparación de tuberías de agua y redes de gas.",
                Lat = (double?)-12.0967,
                Lng = (double?)-77.0360
            },
            new
            {
                Name = "Lucía Pintora",
                Email = "lucia.pint@example.com",
                Role = Roles.Worker,
                AvatarUrl = "https://images.unsplash.com/photo-1580489944761-15a19d654956?w=150",
                IsWorker = true,
                Profession = RequestCategories.Pintura,
                Experience = 5,
                Distance = 3.0,
                Jobs = 29,
                About = "Pintura decorativa para interiores y exteriores con acabados de calidad.",
                Lat = (double?)-12.1088,
                Lng = (double?)-77.0328
            },
            new
            {
                Name = "Pedro Electricista",
                Email = "pedro.elec@example.com",
                Role = Roles.Worker,
                AvatarUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150",
                IsWorker = true,
                Profession = RequestCategories.Electricidad,
                Experience = 4,
                Distance = 4.2,
                Jobs = 18,
                About = "Diagnóstico y mantenimiento de tableros eléctricos y cableado general.",
                Lat = (double?)-12.0433,
                Lng = (double?)-77.0393
            },
            new
            {
                Name = "Jorge Carpintero",
                Email = "jorge.carp@example.com",
                Role = Roles.Worker,
                AvatarUrl = "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=150",
                IsWorker = true,
                Profession = RequestCategories.Carpinteria,
                Experience = 10,
                Distance = 2.8,
                Jobs = 52,
                About = "Mueblería a medida, restauración de puertas, closets y acabados en madera.",
                Lat = (double?)-12.0633,
                Lng = (double?)-77.0167
            }
        };

        foreach (var item in demoSeedData)
        {
            var user = db.Users.Include(u => u.WorkerProfile).FirstOrDefault(u => u.Email == item.Email);
            if (user == null)
            {
                user = new User
                {
                    Name = item.Name,
                    Email = item.Email,
                    Role = item.Role,
                    AvatarUrl = item.AvatarUrl,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = hasher.HashPassword(user, "chamba2026");
                db.Users.Add(user);
                db.SaveChanges();
            }
            else
            {
                var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, "chamba2026");
                if (verify == PasswordVerificationResult.Failed)
                {
                    user.PasswordHash = hasher.HashPassword(user, "chamba2026");
                    db.SaveChanges();
                }
            }

            if (item.IsWorker && user.WorkerProfile == null)
            {
                var worker = new Worker
                {
                    UserId = user.Id,
                    Profession = item.Profession,
                    ExperienceYears = item.Experience,
                    DistanceKm = item.Distance,
                    JobsCount = item.Jobs,
                    About = item.About,
                    Latitude = item.Lat,
                    Longitude = item.Lng,
                    Certifications = new List<string> { "Certificación Técnica ChambaPoint" },
                    Gallery = new List<string>(),
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Workers.Add(worker);
                db.SaveChanges();
            }
            else if (item.IsWorker && user.WorkerProfile != null && user.WorkerProfile.Latitude == null)
            {
                user.WorkerProfile.Latitude = item.Lat;
                user.WorkerProfile.Longitude = item.Lng;
                db.SaveChanges();
            }
        }

        foreach (var w in db.Workers.Where(w => w.Latitude == null || w.Longitude == null))
        {
            w.Latitude = -12.0555;
            w.Longitude = -77.0450;
        }
        db.SaveChanges();

        foreach (var u in db.Users.ToList())
        {
            var verify = hasher.VerifyHashedPassword(u, u.PasswordHash, "chamba2026");
            if (verify == PasswordVerificationResult.Failed)
            {
                u.PasswordHash = hasher.HashPassword(u, "chamba2026");
            }
        }
        db.SaveChanges();

        SeedRequestsAndReviews(db);
    }

    private static void SeedRequestsAndReviews(AppDbContext db)
    {
        var customer = db.Users.FirstOrDefault(u => u.Email == "ana.cli@example.com");
        var carlosWorker = db.Workers
            .Include(w => w.User)
            .FirstOrDefault(w => w.User.Email == "carlos.elec2@example.com");
        var mariaWorker = db.Workers
            .Include(w => w.User)
            .FirstOrDefault(w => w.User.Email == "maria.gas@example.com");

        if (customer == null || carlosWorker == null || mariaWorker == null)
        {
            return;
        }

        if (!db.Reviews.Any(r => r.WorkerId == carlosWorker.Id && r.CustomerId == customer.Id))
        {
            db.Reviews.Add(new Review
            {
                WorkerId = carlosWorker.Id,
                CustomerId = customer.Id,
                Rating = 5,
                Text = "Excelente trabajo en la instalación de los interruptores y tablero. Muy puntual.",
                Photos = new List<string>(),
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            });
        }

        if (!db.Reviews.Any(r => r.WorkerId == mariaWorker.Id && r.CustomerId == customer.Id))
        {
            db.Reviews.Add(new Review
            {
                WorkerId = mariaWorker.Id,
                CustomerId = customer.Id,
                Rating = 5,
                Text = "Reparó la fuga de agua rápidamente y dejó todo limpio. Muy recomendada.",
                Photos = new List<string>(),
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            });
        }

        if (!db.Requests.Any(r => r.CustomerId == customer.Id && r.WorkerId == carlosWorker.Id))
        {
            var req = new Request
            {
                CustomerId = customer.Id,
                WorkerId = carlosWorker.Id,
                Category = RequestCategories.Electricidad,
                Description = "Revisión del sistema eléctrico y cambio de interruptor termomagnético.",
                Photos = new List<string>(),
                Urgency = RequestUrgencies.LoAntesPosible,
                Status = RequestStatuses.Aceptada,
                Price = 85.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };
            db.Requests.Add(req);
            db.SaveChanges();

            if (!db.Appointments.Any(a => a.RequestId == req.Id))
            {
                db.Appointments.Add(new Appointment
                {
                    RequestId = req.Id,
                    WorkerId = carlosWorker.Id,
                    CustomerId = customer.Id,
                    DateTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
                    Description = "Visita técnica para revisión de interruptores.",
                    Status = AppointmentStatuses.Aceptada,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                });
            }

            if (!db.Messages.Any(m => m.RequestId == req.Id))
            {
                db.Messages.AddRange(
                    new Message
                    {
                        SenderId = customer.Id,
                        ReceiverId = carlosWorker.UserId,
                        RequestId = req.Id,
                        Text = "Buenas tardes, ¿podría venir mañana en la tarde?",
                        SentAt = DateTime.UtcNow.AddDays(-1),
                        IsRead = true
                    },
                    new Message
                    {
                        SenderId = carlosWorker.UserId,
                        ReceiverId = customer.Id,
                        RequestId = req.Id,
                        Text = "Hola Ana, sí claro, coordinamos la visita para las 3:00 PM.",
                        SentAt = DateTime.UtcNow.AddHours(-18),
                        IsRead = true
                    }
                );
            }
        }

        db.SaveChanges();
    }
}

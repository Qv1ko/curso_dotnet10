# Revisión arquitectónica de ClinicaMardeva

## 1. Clasificación de los elementos de la tabla A

| #   | Elemento                      | Clasificación                       | Observación                                                                                                                                                                    |
| --- | ----------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | `IScheduleAppointmentUseCase` | **Puerto de entrada**               | Lo invoca el exterior (la API) para activar el caso de uso. Su ubicación en Application/Ports es correcta.                                                                     |
| 2   | `ScheduleAppointmentUseCase`  | **Ninguna (interior del hexágono)** | Implementa el puerto de entrada y orquesta la lógica de aplicación. No es puerto ni adaptador.                                                                                 |
| 3   | `IAppointmentRepository`      | **Puerto de salida**                | Ubicarlo en Domain es defendible (repositorio por agregado, estilo DDD). Si el equipo prefiere que Domain no tenga puertos, puede moverse a Application sin cambiar la lógica. |
| 4   | `SqlAppointmentRepository`    | **Adaptador secundario**            | Implementa un puerto de salida con EF Core.                                                                                                                                    |
| 5   | `AppointmentsController`      | **Adaptador primario**              | Traduce HTTP a una llamada al puerto de entrada.                                                                                                                               |
| 6   | `SmtpNotificationSender`      | **Adaptador secundario**            | Implementa `INotificationSender`, que es un puerto de salida definido en Application.                                                                                          |
| 7   | `Appointment`                 | **Ninguna (núcleo de dominio)**     | Es una entidad, ni puerto ni adaptador.                                                                                                                                        |

## 2. Referencias de la tabla B

| #   | ¿Incumple?                                            | Corrección                                                                                                                                                                                                                                |
| --- | ----------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| R1  | **Sí** (Regla de Dependencia)                         | Eliminar la referencia: el dominio no depende de nadie. `AgendaDbContext` es de Infrastructure, y si el dominio necesita datos los obtiene mediante `IAppointmentRepository`, no reutilizando el contexto.                                |
| R2  | No                                                    | Correcta: Application depende de Domain, hacia el centro.                                                                                                                                                                                 |
| R3  | No                                                    | Correcta: Infrastructure depende hacia dentro e implementa los puertos. Conviene añadir referencia explícita a Domain, pues implementa `IAppointmentRepository`.                                                                          |
| R4  | No, con matiz                                         | Api actúa como raíz de composición y necesita Infrastructure solo para registrar la inyección de dependencias. Es mejor exponer un `AddAgendaInfrastructure()` y que los controladores no usen ningún tipo de Infrastructure.             |
| R5  | **Sí** (límites entre módulos y Regla de Dependencia) | Billing.Application no puede referenciar la Infrastructure de otro módulo ni de ninguna capa externa. Debe referenciar solo un proyecto de contratos públicos de Agenda (`Agenda.Contracts`) y acceder a él a través de un puerto propio. |

## 3. Corrección del fragmento C

El fragmento tiene dos fallos: depende de `AgendaDbContext` (infraestructura ajena) y maneja `Appointment` (entidad de dominio ajena).

**Qué debe exponer Agenda:** un proyecto de contratos públicos (`Agenda.Contracts`) con una interfaz de consulta, implementada internamente por Agenda y registrada en el contenedor de DI.

```csharp
// Agenda.Contracts
public interface IAgendaModuleApi
{
    Task<AppointmentSummary?> GetAppointmentAsync(Guid appointmentId, CancellationToken ct);
}

public sealed record AppointmentSummary(
    Guid Id, Guid PatientId, Guid ProfessionalId,
    DateTimeOffset StartsAt, TimeSpan Duration, string ServiceCode, string Status);
```

**Qué cruza la frontera:** un DTO inmutable (`record`) con solo los datos que Billing necesita. Nunca cruzan la entidad `Appointment`, el `DbContext` ni un `IQueryable`.

**Qué se inyecta en Billing:** un puerto de salida propio, con su modelo propio, implementado por un adaptador anticorrupción que traduce el DTO de Agenda.

```csharp
// Billing.Application/Ports
public interface IAppointmentLookup
{
    Task<BillableAppointment?> FindAsync(Guid appointmentId, CancellationToken ct);
}

public class GenerateInvoiceUseCase(IAppointmentLookup appointments, IInvoiceRepository invoices)
{
    public async Task<Guid> ExecuteAsync(Guid appointmentId, CancellationToken ct)
    {
        var appt = await appointments.FindAsync(appointmentId, ct)
            ?? throw new AppointmentNotFoundException(appointmentId);
        // ... cálculo de la factura
    }
}

// Billing.Infrastructure: adaptador secundario que usa IAgendaModuleApi y mapea a BillableAppointment
```

Así Billing solo depende de `Agenda.Contracts` (desde su Infrastructure), y su núcleo queda aislado de Agenda.

## 4. Decisiones de base de datos

- **D1: admisible.** Es la práctica recomendada: un esquema por módulo en la misma base, sin claves foráneas entre ellos, mantiene la propiedad de los datos y permite extraer un módulo en el futuro.
- **D2: no admisible.** La clave foránea entre esquemas acopla los módulos a nivel de datos y rompe la autonomía. Debe sustituirse por un `AppointmentId` sin FK, validado por la API de Agenda o por eventos de integración.
- **D3: no admisible.** La vista salta la API pública de Agenda y ata a Billing al esquema interno de sus tablas, que Agenda no puede cambiar sin romperlo. Si Billing necesita un modelo de lectura propio, debe construirlo como proyección alimentada por eventos de Agenda o consultar el contrato público.

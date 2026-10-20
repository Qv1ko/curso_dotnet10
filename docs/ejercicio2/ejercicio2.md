# Decisión arquitectónica

## 1. Estilo recomendado: Monolito Modular Orientado a Dominio

Recomiendo un único despliegue dividido en tres módulos (Envíos, Mantenimiento y Facturación), con fronteras estrictas y contratos explícitos entre ellos. No recomiendo los siete microservicios. Además, la tabla solo identifica tres dominios, así que siete servicios ni siquiera tendrían un límite de negocio claro.

## 2. Clasificación de las situaciones

| Situación                                                                                | Clasificación          | Motivo                                                                                                                                                   |
| ---------------------------------------------------------------------------------------- | ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. Cambio de tarifa que obliga a desplegar cinco servicios a la vez y en orden           | Monolito distribuido   | Los servicios no son desplegables independientes, así que se paga la complejidad distribuida sin obtener su beneficio.                                   |
| B. Facturación lee con SQL la tabla Vehiculos de Mantenimiento                           | Falso monolito modular | Se salta el contrato público del módulo y se acopla a su esquema interno, de modo que Mantenimiento ya no puede cambiar su tabla sin romper Facturación. |
| C. Facturación guarda copia de NombreCliente y DireccionFacturacion al emitir la factura | Práctica correcta      | Una factura es un documento inmutable, y el módulo conserva los datos sin depender de la tabla de otro.                                                  |
| D. Envíos no puede responder sin llamar por HTTP síncrono a otros tres servicios         | Monolito distribuido   | Hay acoplamiento temporal y de disponibilidad: si cae uno de los tres, Envíos deja de funcionar, y encima se añade latencia de red.                      |

## 3. Defectos de aislamiento del código propuesto

### Defecto 1

Declaración del proyecto Shipments. ShipmentsDbContext (y sus entidades y DbSet) es público, lo que expone la persistencia interna del módulo a cualquier otro proyecto.

Corrección: hacer internos el contexto, las entidades y los DbSet, de modo que solo el propio módulo pueda usarlos. Lo que Envíos ofrece al resto debe ser una API pública mínima. Conviene también que el módulo use su propio esquema dentro de SQL Server, para que la frontera sea visible también en la base de datos.

### Defecto 2

Proyecto Billing. Facturación referencia directamente el proyecto Shipments, y Invoice tiene una propiedad de navegación a la entidad Shipment. El Include fuerza un JOIN contra una tabla que pertenece a otro módulo, con lo que el límite queda anulado tanto en el código como en los datos.

Corrección:

- Eliminar la referencia a la implementación de Shipments y referenciar solo su proyecto de contratos.
- Eliminar la propiedad de navegación y el Include. Invoice debe guardar únicamente el identificador del envío como valor simple, sin relación de clave foránea entre módulos.

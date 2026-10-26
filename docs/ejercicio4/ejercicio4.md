# Informe de rendimiento: plataforma de reservas (.NET 10)

## 1. Estadísticos de la muestra

La suma de las 20 muestras es 2.000 ms.

Muestra ordenada: 18, 20, 21, 22, 23, 24, 25, 26, 27, 28, 30, 32, 35, 38, 41, 45, 60, 95, **410**, 980.

| Métrica | Cálculo (rango más cercano) | Resultado  |
| ------- | --------------------------- | ---------- |
| Media   | 2.000 / 20                  | **100 ms** |
| p50     | posición ⌈0,50·20⌉ = 10     | **28 ms**  |
| p95     | posición ⌈0,95·20⌉ = 19     | **410 ms** |

**Veredicto: el SLO de p95 (< 400 ms) NO se cumple**, porque 410 ms supera el límite por 10 ms. Como dato adicional, el p99 es la posición 20, es decir 980 ms, y sí cumple el límite de 1.200 ms.

**Media frente a p95:** la media (100 ms) parece sana y es 3,6 veces la mediana, pero el p95 (410 ms) muestra que unas pocas peticiones muy lentas arrastran la media y que el usuario de la cola sufre una experiencia muy distinta a la que sugiere el promedio.

## 2. Ley de Little

L = λ · W = 180 RPS × 0,25 s = **45 peticiones en vuelo**.

**Veredicto: el objetivo de throughput NO se cumple.** El máximo sostenido fue de 180 RPS, por debajo de los 200 RPS exigidos. Para llegar a 200 RPS con la misma latencia media harían falta 200 × 0,25 = 50 peticiones simultáneas.

## 3. Clasificación de incidentes

| Incidente | Categoría                | Recurso saturado                                                                             | Causa típica según el manual                                                                                                                                                    |
| --------- | ------------------------ | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A**     | Cuello de botella de CPU | CPU (96 %). La cola del Thread Pool crece como consecuencia, porque los hilos no dan abasto. | Código intensivo en cómputo o ineficiente en el hot path (algoritmos costosos, regex, serialización, bucles).                                                                   |
| **B**     | Presión de GC            | Tiempo de CPU consumido por el recolector (41 % en pausas).                                  | Tasa de asignación de objetos muy alta (cadenas, copias y objetos intermedios), que dispara recolecciones frecuentes.                                                           |
| **C**     | Fuga de memoria          | Memoria (el heap crece de 400 MB a 3,1 GB hasta el OOM).                                     | Objetos retenidos que nunca se liberan: cachés o colecciones estáticas sin límite, suscripciones a eventos no canceladas, recursos sin `Dispose`.                               |
| **D**     | Dependencia de datos/red | Pool de conexiones a la BD (agotado).                                                        | Consultas lentas (3.500 ms) que retienen las conexiones, como las que no usan un índice adecuado o sufren bloqueos. Las peticiones esperan una conexión libre hasta el timeout. |

Notas de diagnóstico:

- **A:** el GC está al 2 % y las dependencias están sanas, así que el problema es la CPU de la propia aplicación.
- **B:** el working set oscila sin crecer, de modo que no es una fuga. La CPU solo muestra picos porque el tiempo se va en pausas de GC.
- **C:** el GC está al 4 % y la CPU al 8 %, lo que confirma que el problema es la acumulación de memoria y no el comportamiento del recolector.
- **D:** CPU, memoria y GC están en reposo, por lo que la lentitud viene de fuera del proceso.

## 4. Medida correcta para B

**Elegida: (b) reducir la tasa de asignación del hot path**, evitando copias de texto y objetos intermedios. Ataca la causa raíz: si se asigna menos, el GC tiene menos que recolectar y bajan las pausas.

- **(a) Descartada:** ampliar la RAM no reduce la tasa de asignación, y el working set ya oscila sin crecer, lo que indica que la memoria no es el recurso escaso. Un heap mayor solo retrasaría las recolecciones sin eliminar el churn.
- **(c) Descartada:** cambiar a Workstation GC no corrige la causa, que es el exceso de asignaciones. Además, Workstation está pensado para menor concurrencia y normalmente sacrifica throughput frente a Server GC, por lo que con esta carga podría empeorar la situación.

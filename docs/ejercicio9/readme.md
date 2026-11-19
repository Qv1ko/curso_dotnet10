# Gateway de Pagos: análisis de situaciones

**S1: `pagos_a` y `pagos_b` no superan el sondeo, petición sin cabecera.**
Tras 2 fallos consecutivos ambos salen de los destinos disponibles. Sin cabecera se aplica la regla 2 sobre lo que queda: solo `pagos_c` (peso 10, suma 10 > 0). La petición va a `pagos_c` con probabilidad del 100 %, aunque su peso nominal sea el 10 %.

**S2: todos sanos, pesos 50 / 50 / 0, petición sin cabecera.**
Regla 2 con suma 100: `pagos_a` y `pagos_b` reciben el 50 % cada uno. `pagos_c` tiene peso 0 y no puede ser elegido; solo recibiría tráfico con `X-Canal: canary` (regla 1).

**S3: `pagos_c` no supera el sondeo, petición con `X-Canal: canary`.**
La regla 1 solo mira los destinos disponibles y `pagos_c` ya está excluido, así que no hay ninguno con Canal = canary y se pasa a la regla 2: `pagos_a` o `pagos_b` (45 / 45, 50 % cada uno). Nunca va a `pagos_c` y no se devuelve error.

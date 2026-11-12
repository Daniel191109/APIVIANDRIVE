# VIANDRIVE — aplicación MAUI corregida v3

## Cambios incluidos

- La pantalla del pasajero consulta `GET /api/conductores/activos` y muestra conductores con GPS reciente y disponibilidad activa.
- Se mantiene la visualización de los vehículos en el mapa inicial y en el mapa de selección de destino.
- El conductor publica su GPS periódicamente en la API.
- Al aceptar una oferta, el pasajero ve la ruta del conductor hacia el punto de recogida.
- Cuando el conductor marca al pasajero como recogido, el estado pasa a `EnViaje` y el mapa del pasajero cambia a la ruta hacia el destino.
- La cancelación del pasajero y del conductor se envía al servidor y solo se confirma en pantalla cuando la API confirma la operación.
- Se conserva el ID del pasajero al abrir la pantalla del viaje para que la acción de cancelar pueda validar al participante correcto.

## Requisitos de integración

1. Primero debe desplegarse el backend incluido en el paquete complementario.
2. Ejecuta en la base de datos SQL Server de Viandrive el script `SQL/Viandrive_Tracking_Cancelaciones.sql` que viene en el backend. Haz una copia de seguridad antes de ejecutar cambios de esquema.
3. Compila la solución `ViandriveApp.slnx` o el proyecto `ViandriveApp.csproj` desde Visual Studio con la carga de trabajo .NET MAUI y Android instalada.
4. En el perfil del conductor, activa **Disponible**, concede el permiso de ubicación y mantén abierta la pantalla del mapa del conductor para publicar la posición GPS.

## Notificaciones de cancelación

La sincronización incluida consulta el estado del viaje periódicamente (aprox. cada 2,5 segundos) mientras las pantallas del viaje están abiertas. El otro participante verá el aviso al recibir el nuevo estado. Esto no es una notificación push cuando la aplicación está cerrada o el teléfono está desconectado.

## Validación

Se validó la estructura XML de las páginas XAML y la sintaxis JavaScript de los mapas. Este entorno no dispone del SDK de .NET MAUI, por lo que no fue posible ejecutar una compilación Android ni instalar la APK desde aquí.

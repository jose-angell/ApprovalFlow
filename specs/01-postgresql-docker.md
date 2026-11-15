# SPEC 01 — PostgreSQL local con Docker Compose

> **Estado:** aprobado
> **Depende de:** —
> **Fecha:** 2026-11-15
> **Objetivo:** Levantar una base de datos PostgreSQL 17 local con Docker Compose, configurada mediante variables de un archivo `.env` y con datos persistidos en un volumen con nombre.

## Alcance

**Dentro:**

- `docker-compose.yml` en la raíz del repo con un único servicio `postgres`.
- Imagen `postgres:17-alpine`.
- Variables `DB_NAME`, `DB_USER`, `DB_PASSWORD` y `DB_PORT` leídas desde `.env`.
- Volumen con nombre `approvalflow_pgdata` para persistir los datos.
- Archivo `.env` local (ignorado por git) con valores de desarrollo.
- Archivo `.env.example` versionado con las mismas variables y valores de ejemplo.

**Fuera de alcance (para futuras specs):**

- Healthcheck del servicio (`pg_isready`).
- Contenedor de pgAdmin u otra interfaz de administración.
- Paquete `Npgsql.EntityFrameworkCore.PostgreSQL` y connection string en `appsettings.json`.
- Referencia del proyecto API a Infrastructure y registro del DbContext.
- Migraciones de EF Core.
- Configuración para entornos distintos de desarrollo local.

## Modelo de datos

Esta feature no introduce estructuras de datos de dominio. Solo define configuración de infraestructura.

Variables de entorno (`.env`):

```dotenv
DB_NAME=approvalflow
DB_USER=approvalflow_user
DB_PASSWORD=approvalflow_dev
DB_PORT=5432
```

`.env.example` contiene las mismas cuatro claves. `DB_PASSWORD` lleva un valor de marcador (`cambiar_esta_contrasena`).

Mapeo en el servicio `postgres`:

| Variable `.env` | Uso en `docker-compose.yml`            |
| --------------- | -------------------------------------- |
| `DB_NAME`       | `POSTGRES_DB`                          |
| `DB_USER`       | `POSTGRES_USER`                        |
| `DB_PASSWORD`   | `POSTGRES_PASSWORD`                    |
| `DB_PORT`       | Puerto del host en `"${DB_PORT}:5432"` |

Otros valores fijos del servicio:

- `container_name: approvalflow-postgres`
- `restart: unless-stopped`
- Volumen: `approvalflow_pgdata:/var/lib/postgresql/data`, declarado en la sección `volumes:` de nivel superior.

## Plan de implementación

1. Crear `.env.example` en la raíz con las cuatro variables y valores de ejemplo.
2. Crear `.env` en la raíz con los valores de desarrollo. Comprobar con `git status` que no aparece como archivo sin seguimiento.
3. Crear `docker-compose.yml` en la raíz con el servicio `postgres`, el mapeo de variables, el puerto, `container_name`, `restart` y el volumen con nombre. Comprobar con `docker compose config` que las variables se sustituyen.
4. Levantar el servicio con `docker compose up -d` y verificar la conexión con `docker exec -it approvalflow-postgres psql -U approvalflow_user -d approvalflow -c "SELECT version();"`.

## Criterios de aceptación

- [ ] Existen `docker-compose.yml`, `.env` y `.env.example` en la raíz del repo.
- [ ] `git status` no muestra `.env`; sí muestra `.env.example` y `docker-compose.yml`.
- [ ] `docker compose config` muestra `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` y el puerto con los valores del `.env`.
- [ ] `docker compose up -d` deja el contenedor `approvalflow-postgres` en estado `running`.
- [ ] `psql` dentro del contenedor conecta con `approvalflow_user` a la base `approvalflow` y `SELECT version();` devuelve PostgreSQL 17.
- [ ] El puerto `5432` del host acepta conexiones (p. ej. `Test-NetConnection localhost -Port 5432` devuelve `TcpTestSucceeded : True`).
- [ ] Tras crear una tabla de prueba, `docker compose down` y `docker compose up -d`, la tabla sigue existiendo.
- [ ] `docker volume ls` lista un volumen cuyo nombre termina en `approvalflow_pgdata`.
- [ ] Cambiar `DB_PORT` en `.env` y recrear el contenedor cambia el puerto expuesto en el host.

## Decisiones

- **Sí:** `postgres:17-alpine`. Versión mayor fijada y imagen ligera; evita cambios inesperados de `latest`.
- **No:** `postgres:latest`. Una actualización mayor rompería el volumen existente.
- **Sí:** volumen con nombre `approvalflow_pgdata`. Docker gestiona permisos y queda fuera de la carpeta de OneDrive.
- **No:** bind mount en una carpeta del repo. OneDrive sincronizaría los archivos de datos y podría corromperlos.
- **Sí:** archivos en la raíz del repo. `docker compose` carga `.env` automáticamente sin flags.
- **Sí:** `.env` local más `.env.example` versionado. El secreto no entra en git y otros desarrolladores saben qué definir.
- **Sí:** `container_name: approvalflow-postgres` y `restart: unless-stopped`. Nombre estable para `docker exec`; se levanta con Docker Desktop salvo parada manual.
- **Sí:** `DB_PORT` solo cambia el puerto del host; el puerto interno sigue siendo `5432`.
- **No:** healthcheck en esta spec. Se añadirá si un servicio dependiente lo necesita.
- **No:** pgAdmin. Añade otro servicio y variables; no es necesario para arrancar.
- **No:** conectar la API a la base de datos. Requiere Npgsql, connection string y referencias entre proyectos; va en su propia spec.

## Riesgos

| Riesgo                                                                  | Mitigación                                                                                              |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| Ya existe un PostgreSQL local escuchando en `5432`                      | Cambiar `DB_PORT` en `.env` (p. ej. `5433`) y recrear el contenedor.                                    |
| Cambiar `DB_USER`/`DB_PASSWORD` tras el primer arranque no tiene efecto | La imagen solo inicializa con un volumen vacío. Borrar con `docker compose down -v` (pierde los datos). |
| `.env` subido por error a git                                           | `.env` ya está en `.gitignore` (línea 46); verificarlo en el paso 2.                                    |

## Qué **no** está en esta spec

- Healthcheck.
- pgAdmin.
- Npgsql, connection string y DI del DbContext en la API.
- Migraciones de EF Core.
- Entornos distintos de desarrollo local.

Cada uno de estos puntos, si se aborda, va en su propia spec.

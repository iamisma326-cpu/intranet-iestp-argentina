# 🚀 Guía de Comandos Git y Flujo de Trabajo

Esta guía contiene todos los comandos esenciales de Git para trabajar con este repositorio conectado a **GitHub** (`origin`), gestionar cambios, trabajar con ramas (**`master`**, **`main`** o ramas secundarias) y entender el impacto en el despliegue automático.

---

## 📌 Datos del Repositorio Remoto
- **Repositorio:** `Login_Administracion_de_Modulos`
- **Remoto configurado (`origin`):** `https://github.com/paulocarreons-max/Login_Administracion_de_Modulos.git`
- **Rama principal actual:** `master`
- **Integración continua:** Despliegue automático a MonsterASP al hacer push a la rama configurada en `.github/workflows/deploy.yml`.

---

## ⚡ Flujo de Trabajo Diario (Lo que más usarás)

### 1. Verificar el estado de tus archivos
Antes de hacer nada, revisa qué archivos han sido modificados o agregados:
```bash
git status
```

### 2. Preparar los archivos para el commit
Agrega todos los archivos modificados al área de preparación (stage):
```bash
git add .
```
> O si deseas agregar un archivo específico:
> ```bash
> git add Ruta/Del/Archivo.ext
> ```

### 3. Guardar los cambios localmente (Commit)
Realiza el commit con un mensaje descriptivo de lo que hiciste:
```bash
git commit -m "Descripción clara de los cambios realizados"
```

### 4. Subir los cambios a GitHub (Push)
Sube tus cambios a la rama en la que estés trabajando:

- **Si estás en la rama `master`:**
  ```bash
  git push origin master
  ```

- **Si estás en otra rama (ejemplo `develop` o `main`):**
  ```bash
  git push origin <nombre-de-tu-rama>
  ```

- **Si es la primera vez que subes una nueva rama a GitHub:**
  ```bash
  git push -u origin <nombre-de-tu-rama>
  ```
  *(El parámetro `-u` vincula tu rama local con la remota, así en el futuro solo necesitarás hacer `git push`).*

---

## 🔄 Actualizar tu Repositorio Local con los Cambios de GitHub (Pull)

Si otro integrante del equipo subió cambios o modificaste código desde GitHub, descárgalos con:

- **Para la rama `master`:**
  ```bash
  git pull origin master
  ```

- **Para cualquier otra rama activa:**
  ```bash
  git pull origin <nombre-de-tu-rama>
  ```

- **Consultar qué ramas y cambios existen en GitHub sin combinarlos de inmediato:**
  ```bash
  git fetch origin
  ```

---

## 🌿 Gestión de Ramas (Branches)

Las ramas permiten trabajar en nuevas características sin alterar el código estable.

### Ver en qué rama estás y cuáles existen
```bash
git branch        # Ver ramas locales (* indica la activa)
git branch -a     # Ver ramas locales y remotas de GitHub
```

### Crear y cambiar a una nueva rama
```bash
# Opción moderna:
git switch -c feature/nuevo-modulo

# Opción clásica:
git checkout -b feature/nuevo-modulo
```

### Cambiar entre ramas existentes
```bash
# Cambiar a master:
git switch master
# o: git checkout master

# Cambiar a otra rama:
git switch <nombre-de-la-rama>
```

### ¿Qué pasa si el proyecto cambia de rama principal? (ej. de `master` a `main`)
Si en GitHub o en el equipo deciden renombrar la rama principal de `master` a `main`:
```bash
# 1. Renombrar la rama local actual a main
git branch -M main

# 2. Subir la nueva rama main a GitHub estableciendo el tracking
git push -u origin main

# 3. (Importante) Recuerda actualizar en .github/workflows/deploy.yml la línea de la rama a 'main'
```

---

## 🔀 Fusionar Cambios entre Ramas (Merge)

Cuando termines una funcionalidad en una rama secundaria y quieras integrarla a `master`:

```bash
# 1. Ve a la rama de destino (ejemplo master)
git switch master

# 2. Asegúrate de tener la última versión de GitHub
git pull origin master

# 3. Fusiona los cambios de tu rama secundaria
git merge feature/nuevo-modulo

# 4. Sube la rama master actualizada a GitHub
git push origin master
```

---

## 📦 Guardar Cambios Temporales sin hacer Commit (Stash)

Si necesitas cambiar de rama urgentemente pero tienes cambios a medias que no quieres perder ni commitear aún:

```bash
# Guardar los cambios pendientes en el casillero temporal
git stash

# Volver a recuperarlos cuando regreses a tu rama
git stash pop
```

---

## ⏪ Deshacer o Descartar Cambios Locales

- **Descartar cambios en un archivo específico que aún no has agregado:**
  ```bash
  git restore NombreDelArchivo.cs
  ```

- **Descartar todos los cambios locales no commiteados:**
  ```bash
  git restore .
  ```

- **Ver el historial resumido de commits:**
  ```bash
  git log --oneline -n 10
  ```

---

## ⚙️ Despliegue Automático CI/CD (MonsterASP)

> [!IMPORTANT]
> El proyecto cuenta con un flujo de integración continua en [deploy.yml](file:///.github/workflows/deploy.yml).
>
> - Actualmente está configurado para ejecutarse en cada `push` hacia la rama **`master`**.
> - Al ejecutar `git push origin master`, GitHub Actions:
>   1. Descarga el código del repositorio.
>   2. Configura el SDK de **.NET 10**.
>   3. Compila el proyecto en modo `Release`.
>   4. Sube los archivos compilados por **FTP a MonsterASP** al directorio `/wwwroot/`.
>
> **Nota:** Si en algún momento cambian de rama principal (por ejemplo a `main`), deben actualizar la línea 6 de `.github/workflows/deploy.yml` para que apunte a `main`.

---

## 📋 Resumen Rápido ("Machete" de Comandos)

| Acción | Comando |
| :--- | :--- |
| Ver estado | `git status` |
| Agregar todo | `git add .` |
| Guardar commit | `git commit -m "Mensaje"` |
| Subir a master | `git push origin master` |
| Subir a otra rama | `git push origin <rama>` |
| Bajar cambios de master | `git pull origin master` |
| Crear nueva rama | `git checkout -b <rama>` |
| Cambiar de rama | `git switch <rama>` |
| Ver ramas | `git branch -a` |
| Ver historial | `git log --oneline -n 5` |

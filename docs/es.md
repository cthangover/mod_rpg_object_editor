# Editor de objetos RPG — los objetos del mundo RPG en un formulario

El Editor de objetos RPG edita los objetos del mundo RPG: los materiales del suelo, las paredes y los tramos de valla, los objetos sobre la rejilla y los personajes. Cada objeto tiene imágenes, un área sobre la rejilla, un ancla (el punto sobre el que se apoya), estados, variantes, luces, una ruta opcional y una interacción opcional. Una vista previa junto al formulario dibuja el objeto en las proyecciones **desde arriba** e **isométrica**, con el renderizador del propio juego. **Guardar** escribe el archivo del mod y el juego usa el objeto de inmediato. Para el trabajo normal no hace falta JSON; el cuadro **Otros campos** queda para usuarios avanzados.

Se abre desde la caja de herramientas (el martillo de la barra) como **Objetos RPG**.

## Un objeto en el editor

La lista de la izquierda muestra los objetos del mod elegido, con una miniatura de cada uno. El formulario de la derecha edita el seleccionado. Aquí está el barril de suministros de RPG Plus:

![La ventana de objetos RPG sobre rpg_plus_game: la lista de sus objetos con el barril de suministros seleccionado, y su formulario: Id rpg_plus/container/supply_barrel, Form sprite, el nombre con un texto en cada idioma, Category container y Extends village_pack/container/barrel.](assets/object-own-form.jpg)

La barra tiene **Guardar** (Ctrl+S), **Nuevo** (Ctrl+N), **Copiar** (Ctrl+D), **Eliminar**, **Deshacer** y **Rehacer** (Ctrl+Z, Ctrl+Y). Un objeto nuevo es un sprite sin imagen todavía, y su id empieza por el nombre del mod seguido de `new_object`.

## Los formularios

Los campos dependen de **Form**, que se elige arriba del formulario:

- **Surface** — un material del suelo: su imagen (un cuadrado del tamaño de parche del mapa), **Mezclar con los vecinos** y el ancho de la mezcla, **Agua** y la imagen de sus variantes.
- **Segment** — una celda de pared o valla: su imagen, **Connect** (los segmentos con la misma clave se unen), **Along** (la dirección del plano vertical en isométrica), la **altura** en celdas y la imagen de sus variantes.
- **Sprite** — un objeto sobre la rejilla: sus **partes** (imágenes), su **área** (ancho y alto en celdas, y la máscara), su **ancla**, indicadores, interacción, estados, variantes, luces y ruta.
- **Actor** — un personaje sobre una celda: su **Charset** (la hoja de caminar), **Facing**, **Column**, interacción, estados, variantes, luces y ruta.

Todos los formularios tienen **Id**, **Name** (un texto en cada idioma del mod, guardado en los archivos de idioma), **Category** (una etiqueta para la paleta: `container`, `vegetation`, `building`…) y **Extends**, el id de un objeto padre.

## Imágenes e importación de PNG

**Importar PNG...** (en el bloque *Imagen*) copia un PNG de su disco a `rpg_images/<categoría>/` del mod editado y lo pone como imagen del objeto: su primera parte, su superficie o su segmento. No se genera nada. El archivo se comprueba antes:

- un archivo que no es PNG se rechaza y no se copia nada;
- una parte de sprite debería medir un número entero de celdas del mapa en cada lado (el tamaño de celda por defecto es 32 píxeles); otros tamaños se importan con un aviso de que las partes se cortarán o dejarán huecos;
- una superficie debería ser un cuadrado del tamaño de parche del mapa (128 píxeles por defecto); otros tamaños se importan con aviso;
- un nombre que ya existe en `rpg_images/<categoría>/` se rechaza: elija otro nombre de archivo.

Las imágenes de las **partes** y de las **variantes** también se eligen del grupo con el botón **…**.

## Partes (sprite)

El bloque **Partes** enumera las imágenes del objeto, un bloque por parte: su **Id**, **Image** (selector o ruta escrita), **Geometry** (`upright` se apoya en su ancla, `ground` yace en el suelo, `plane` es un plano vertical, `quad` es un cuadrilátero libre), **Along** (para `plane`), **Crop** (x, y, w, h en píxeles de la imagen; vacío es la imagen entera), **Desplazamiento** (x, y en celdas), **Tamaño** (w, h en celdas), **Esquinas** (cuatro puntos `x, y, z` para `quad`) y **Se muestra en** (una proyección o ambas). **Añadir parte**, **Subir**, **Bajar** y **Quitar parte** cambian el orden. La tabla muestra las partes propias del objeto; las partes que hereda del padre por **Extends** el juego las dibuja, pero todavía no aparecen en la tabla.

## Área y solidez

**Ancho** y **Alto** (celdas) fijan el área. La rejilla bajo ellos es la **máscara**: un clic cambia una celda entre `#` (bloquea el paso) y `.` (libre). Un tamaño nuevo devuelve la máscara a la solidez del objeto. **Indicadores** contiene **Bloquea el paso** (según la forma, sí o no), **Oculto** (no se dibuja, no es sólido, no es interactivo), **Agua** y **Mezclar con los vecinos** para superficies, y el **Orden de dibujo** de las partes.

## Ancla

El ancla es el punto del objeto por el que el juego ordena y coloca el objeto. En cualquiera de las vistas previas, pulse y arrastre el marcador amarillo: la celda bajo el puntero pasa a ser el ancla, en su centro (los valores se escriben en celdas desde la esquina superior izquierda del área, por ejemplo `1.5, 2.5`). El campo **Ancla** del formulario guarda el mismo valor y también se puede escribir.

## Interacción

El bloque **Interacción** fija lo que el jugador activa en el objeto:

- **Disparador**: `Use` (la tecla de acción junto a él) o `Step` (pisar su celda).
- **Elegir escenario...** abre la lista de escenarios de todos los mods cargados. Un escenario y un teletransporte no pueden estar a la vez: elegir uno quita el otro.
- **Elegir mapa...** con **Columna**, **Fila** y **Mirada al llegar** fija un teletransporte a una celda de otro mapa.
- **Líneas de guion**: comandos, uno por línea, que se ejecutan al activarse.
- **Quitar interacción** quita el bloque entero.

## Estados

Un estado cambia el objeto mientras se cumple su condición: un cofre que se abre, una puerta que se desbloquea. El bloque **Estados** los enumera, con **Añadir estado**, **Quitar estado** y **Subir / Bajar**. Cada estado tiene un **Id**, una **Condición** (por ejemplo `self.open == 1`; gana el último estado cuya condición se cumple), **Facing**, **Oculto**, **Bloquea el paso**, sus propias partes y su propia interacción. La opción **Mostrar** sobre la vista previa cambia la vista a un estado, de modo que se ve cada estado como lo dibuja el juego.

## Variantes

Una variante es un objeto parcial para una estación o un aspecto: un árbol de invierno, una puerta rota. **Variantes** las enumera; **Nombre de la variante** y **Añadir variante** crean una, y **Quitar variante** elimina la seleccionada. La variante de un sprite edita sus propias partes; la de una superficie o un segmento toma una imagen. **Mostrar** elige una variante en la vista previa.

## Luces y ruta

**Luz principal** da al objeto una luz propia (radio en celdas, color cuyo alfa es la intensidad, posición en celdas). **Añadir luz** añade más luces con sus propias posiciones. **Se mueve por una ruta** hace que el objeto recorra una ruta fija: sus **Pasos** (separados por comas: `up`, `down`, `left`, `right`, `random`, `toward`, `away`, `wait`, `face_up` … `face_right`), **Segundos por paso** y **Repetir**.

## Objetos de otros mods

**Entradas de otros mods** (marcado por defecto) enumera los objetos de las dependencias del mod, marcados con su dueño: `↗ rpg`. El archivo del dueño nunca se escribe. Editar un objeto así escribe en su mod un archivo de parche, `patches/rpg_object/<su mod>.json`, que contiene solo los valores que cambió. El juego usa el objeto del dueño con su parche encima.

- **Restablecer a lo heredado** quita su parche del objeto, y el juego vuelve a ver los valores del dueño.
- **Copiar** un objeto ajeno crea un objeto propio con un id nuevo que puede cambiar libremente.
- Eliminar un objeto ajeno escribe una entrada `$delete` en el parche.

![Un objeto ajeno: el barril de village_pack encontrado con la búsqueda, con la marca de su dueño en la lista; la nota sobre el formulario dice que sus cambios se guardan como parche en rpg_plus_game y que los archivos de village_pack no cambian.](assets/object-foreign-form.jpg)

## Vista previa

La vista previa está bajo los campos y muestra el objeto seleccionado tal como lo ve el juego, incluidos los cambios aún no guardados. **Mostrar** elige el aspecto normal, una variante o un estado. El selector de proyección muestra **Ambas** una junto a otra, solo **Desde arriba** o solo **Isométrica**. La vista previa dibuja el objeto en un pequeño mapa de muestra, no en el mapa del mundo.

## Usar un objeto

- Un sprite o un actor se coloca en un mapa con el Editor de mapas RPG: su paleta muestra los sprites en la pestaña **Sprites** y los actores en la pestaña **Actores**. Un objeto guardado aparece allí de inmediato.
- Un sprite con interacción inicia su escenario, o teletransporta al jugador, cuando el jugador lo usa o pisa.

## Pendiente

Esto no está en esta versión:

- las partes heredadas por **Extends** no aparecen en la tabla de partes (el juego y la vista previa sí las dibujan);
- pintar el área con un pincel directamente en la vista previa (de momento el editor es la rejilla de máscara del formulario);
- los materiales vecinos en la vista previa de superficies, y la figura del jugador en la vista previa de sprites;
- miniaturas de los objetos padre en **Extends**;
- un botón que crea un hijo de un objeto: fije **Extends** a mano.

## Si algo sale mal

1. **El objeto no aparece.** Está en otro mod, o la lista está filtrada: borre la búsqueda o active **Entradas de otros mods** para un objeto del dueño.
2. **La vista previa muestra una sola proyección, o una nota dice que una proyección no está cargada.** El mod de renderizado de esa proyección no está en el juego.
3. **Un cambio no se ve en el juego.** Guarde primero (Ctrl+S). Solo los archivos guardados llegan al juego.
4. **Se rechaza una importación de PNG.** Su mensaje dice por qué: no es PNG, la imagen está vacía o el nombre ya existe. En ese caso no se copia nada.
5. **Un objeto de otro mod no cambia.** Su archivo no se escribe; el parche en su mod lleva el cambio, y **Restablecer a lo heredado** lo quita.

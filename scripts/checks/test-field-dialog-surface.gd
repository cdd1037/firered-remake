# Native rendering regression. Requires a display/renderer, not --headless.
# The control owns wrapping/clipping; this only checks compositing and freshness.
extends SceneTree

class Painter extends Node2D:
 var texture: Texture2D
 var cover := false
 func _draw():
  if texture: draw_texture(texture, Vector2.ZERO)
  if cover: draw_rect(Rect2(0, 0, 240, 160), Color.BLACK)

var surface
var text
var painter

func _initialize():
 call_deferred("run")

func verify(ok: bool, message: String):
 if not ok:
  push_error("FIELD_SURFACE_FAIL: " + message)
  quit(1)
 return ok

func ink(image: Image):
 var count := 0
 for y in range(image.get_height()):
  for x in range(image.get_width()):
   if image.get_pixel(x, y).a > 0.01: count += 1
 return count

func rendered():
 painter.texture = surface.call("PrepareTexture")
 painter.queue_redraw()
 await process_frame
 await RenderingServer.frame_post_draw

func run():
 surface = load("res://SourceDialogSurface.cs").new()
 root.add_child(surface)
 text = surface.get("Text")
 painter = Painter.new()
 root.add_child(painter)
 text.call("Bind", "FIRST\nSECOND\nTHIRD\nFOURTH\nFIFTH", ThemeDB.fallback_font, 12, 15, 198, 2, false)
 text.call("RevealAll")
 await rendered()
 if not verify(surface.size == Vector2i(198, 30), "first draw sizes viewport"): return
 if not verify(ink(surface.get_texture().get_image()) > 0, "first rendered frame contains text"): return
 var first = surface.get_texture().get_image().get_data()
 text.call("TryAdvanceVisual", true)
 await rendered()
 if not verify(ink(surface.get_texture().get_image()) == 0, "new page clears previous reveal without stale pixels"): return
 text.call("RevealAll")
 await rendered()
 if not verify(first != surface.get_texture().get_image().get_data(), "next page changes texture"): return
 text.call("Clear")
 await rendered()
 if not verify(ink(surface.get_texture().get_image()) == 0, "clear removes retained pixels"): return
 text.visible = true
 text.call("Bind", "A fresh wider window", ThemeDB.fallback_font, 12, 16, 212, 2, false)
 text.call("RevealAll")
 await rendered()
 if not verify(surface.size == Vector2i(212, 32) and ink(surface.get_texture().get_image()) > 0, "resize/rebind renders immediately"): return
 painter.cover = true
 await rendered()
 var screen = root.get_texture().get_image()
 var covered = true
 for y in range(32):
  for x in range(212):
   var pixel = screen.get_pixel(x, y)
   if pixel.r != 0 or pixel.g != 0 or pixel.b != 0: covered = false
 if not verify(covered, "later parent overlay covers all native text pixels"): return
 surface.queue_free()
 painter.queue_free()
 await process_frame
 print("FIELD_SURFACE_PASS: first frame, visual-page clear, next page, Clear/rebind, resize and overlay order")
 quit(0)

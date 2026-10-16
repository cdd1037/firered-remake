# Runs against the built project with Godot --headless; no C# compilation or
# duplicate layout setup. Load Bootstrap, then inspect its real shared player/
# rival editor. Isolate XDG paths and disable audio as documented in opening-
# presentation.md. No save operation or gameplay command is submitted.
extends SceneTree

func _initialize():
 call_deferred("run")

func fail(message: String):
 push_error("NAME_EDITOR_LAYOUT_FAIL: " + message)
 quit(1)

func run():
 var scene = load("res://Bootstrap.tscn").instantiate()
 root.add_child(scene)
 var edit: LineEdit
 var deadline = Time.get_ticks_msec() + 30000
 while Time.get_ticks_msec() < deadline:
  for candidate in scene.find_children("*", "LineEdit", true, false):
   if candidate.max_length == 7:
    edit = candidate
    break
  if edit != null:
   break
  await process_frame
 if edit == null:
  fail("No shared player/rival LineEdit was created before startup deadline.")
  return
 edit.visible = true
 edit.text = "RED"
 edit.grab_focus()
 edit.edit()
 edit.select_all()
 await process_frame
 if edit.position != Vector2(94,47) or edit.size != Vector2(104,18):
  fail("Unexpected bounds %s / %s; minimum %s" % [edit.position, edit.size, edit.get_minimum_size()])
  return
 if edit.get_minimum_size().y != 14 or not edit.has_focus() or not edit.has_selection():
  fail("Unexpected source font minimum or native editing state.")
  return
 if edit.get_theme_stylebox("normal").content_margin_top != 0 or edit.get_theme_stylebox("read_only").content_margin_top != 0:
  fail("Default LineEdit margins leaked into the source name panel.")
  return
 if edit.get_theme_stylebox("normal").bg_color.a != 0 or edit.get_theme_color("font_color") != Color.WHITE or edit.get_theme_constant("outline_size") != 0:
  fail("The source panel/font received an extra fill, modulation, or outline.")
  return
 edit.delete_text(edit.get_selection_from_column(), edit.get_selection_to_column())
 edit.caret_column = 0
 edit.insert_text_at_caret("abcdefgh")
 if edit.text != "abcdefg":
  fail("The native seven-character text cap changed.")
  return
 print("NAME_EDITOR_LAYOUT_PASS: actual compiled shared LineEdit bounds=",edit.get_rect()," minimum=",edit.get_minimum_size()," native focus/selection/max7 preserved")
 quit()

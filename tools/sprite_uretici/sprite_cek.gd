extends SceneTree
## KayKit 3D modellerini 2D sprite karelerine çevirir.
## Kullanım: xvfb-run godot --path . --rendering-driver opengl3 -s sprite_cek.gd
## Çıktı: cikti/<karakter>/<animasyon>_<kare>.png

const BOYUT := 320
const FPS := 12.0

var KARAKTERLER := [
	{"ad": "okcu", "glb": "Rogue", "goster": ["2H_Crossbow"], "anim": [
		["bekle", "2H_Ranged_Aiming", 8], ["saldiri", "2H_Ranged_Shoot", 0], ["beceri", "Spellcast_Raise", 0],
		["sevinc", "Cheer", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "buyucu", "glb": "Mage", "goster": ["2H_Staff"], "anim": [
		["bekle", "2H_Melee_Idle", 0], ["saldiri", "Spellcast_Shoot", 0], ["beceri", "Spellcast_Long", 0],
		["sevinc", "Cheer", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "barbar", "glb": "Barbarian", "goster": ["1H_Axe", "Barbarian_Hat", "Barbarian_Cape"], "anim": [
		["bekle", "Idle", 0], ["saldiri", "Throw", 0], ["beceri", "Jump_Full_Short", 0],
		["sevinc", "Cheer", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "sovalye", "glb": "Knight", "goster": ["1H_Sword", "Round_Shield", "Knight_Helmet", "Knight_Cape"], "anim": [
		["bekle", "Idle", 0], ["saldiri", "1H_Melee_Attack_Chop", 0], ["beceri", "Block_Attack", 0],
		["sevinc", "Cheer", 0], ["darbe", "Hit_A", 0], ["olum", "Death_A", 0]]},
	{"ad": "iskelet", "glb": "Skeleton_Minion", "goster": [], "anim": [
		["yuru", "Walking_D_Skeletons", 0], ["saldiri", "Unarmed_Melee_Attack_Punch_A", 0],
		["olum", "Death_C_Skeletons", 0], ["dogus", "Spawn_Ground_Skeletons", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "savasci", "glb": "Skeleton_Warrior", "goster": [], "anim": [
		["yuru", "Walking_A", 0], ["saldiri", "1H_Melee_Attack_Chop", 0],
		["olum", "Death_A", 0], ["dogus", "Spawn_Ground_Skeletons", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "haydut", "glb": "Skeleton_Rogue", "goster": [], "anim": [
		["yuru", "Running_C", 0], ["saldiri", "Unarmed_Melee_Attack_Kick", 0],
		["olum", "Death_B", 0], ["dogus", "Spawn_Ground_Skeletons", 0], ["darbe", "Hit_A", 0]]},
	{"ad": "kara_buyucu", "glb": "Skeleton_Mage", "goster": [], "anim": [
		["yuru", "Walking_B", 0], ["saldiri", "Spellcast_Shoot", 0],
		["olum", "Death_A", 0], ["dogus", "Spawn_Air", 0], ["darbe", "Hit_A", 0]]},
]

var vp: SubViewport
var kok: Node3D


func _initialize() -> void:
	vp = SubViewport.new()
	vp.size = Vector2i(BOYUT, BOYUT)
	vp.transparent_bg = true
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.msaa_3d = Viewport.MSAA_4X
	root.add_child(vp)
	var ortam := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.75, 0.78, 0.9)
	e.ambient_light_energy = 0.75
	ortam.environment = e
	vp.add_child(ortam)
	var isik := DirectionalLight3D.new()
	isik.light_energy = 1.25
	isik.rotation_degrees = Vector3(-40, 35, 0)
	vp.add_child(isik)
	var kam := Camera3D.new()
	kam.projection = Camera3D.PROJECTION_ORTHOGONAL
	kam.size = 3.4
	kam.position = Vector3(0, 1.45, 10)
	vp.add_child(kam)
	kok = Node3D.new()
	vp.add_child(kok)
	_calis.call_deferred()


func _calis() -> void:
	var secim := OS.get_environment("SADECE")
	var meta := {}
	for k in KARAKTERLER:
		if secim != "" and k["ad"] != secim:
			continue
		meta[k["ad"]] = await _karakter(k)
	var f := FileAccess.open("res://cikti/meta_%s.json" % (secim if secim != "" else "hepsi"), FileAccess.WRITE)
	f.store_string(JSON.stringify(meta, "  "))
	f.close()
	quit()


func _karakter(k: Dictionary) -> Dictionary:
	var model: Node3D = (load("res://m/%s.glb" % k["glb"]) as PackedScene).instantiate()
	kok.add_child(model)
	model.rotation_degrees.y = 62.0  # sağa bakar, hafif kameraya döner
	# Silah/aksesuar: sadece istenenler görünsün
	for mi in model.find_children("*", "MeshInstance3D", true, false):
		var bagli := mi.get_parent() is BoneAttachment3D
		var aksesuar: bool = mi.name.begins_with("Skeleton") or mi.name.ends_with("_Hat") or mi.name.ends_with("_Cape") or mi.name.ends_with("_Helmet")
		if bagli and not aksesuar and not (mi.name in k["goster"]):
			mi.visible = false
	var ap: AnimationPlayer = model.find_children("*", "AnimationPlayer", true, false)[0]
	var klasor: String = "res://cikti/" + str(k["ad"])
	DirAccess.make_dir_recursive_absolute(klasor)
	var bilgi := {}
	for a in k["anim"]:
		var cikis: String = a[0]
		var ad: String = a[1]
		if not ap.has_animation(ad):
			push_error("animasyon yok: %s %s" % [k["ad"], ad])
			continue
		var anim := ap.get_animation(ad)
		var kare: int = int(a[2]) if int(a[2]) > 0 else maxi(4, int(round(anim.length * FPS)))
		kare = mini(kare, 24)
		ap.play(ad)
		for i in kare:
			var t := anim.length * float(i) / float(kare)
			if cikis == "olum" or cikis == "dogus":
				t = anim.length * float(i) / float(kare - 1)
			ap.seek(t, true)
			await process_frame
			await RenderingServer.frame_post_draw
			var img := vp.get_texture().get_image()
			img.save_png("%s/%s_%02d.png" % [klasor, cikis, i])
		bilgi[cikis] = {"kare": kare, "sure": anim.length}
		print(k["ad"], " ", cikis, " ", kare)
	model.queue_free()
	await process_frame
	return bilgi

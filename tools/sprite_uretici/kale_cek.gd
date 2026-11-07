extends SceneTree
## Kaleyi 3D taşlardan "modelleyip" 2D sprite parçalarına çevirir; ok ve baltayı da çizer.
## Çıktı: cikti/kale/*.png

const PPU := 144.0  # birim başına piksel (sonra yarıya küçülür)

var vp: SubViewport
var kam: Camera3D
var kok: Node3D
var tas: StandardMaterial3D
var tas_koyu: StandardMaterial3D
var tahta: StandardMaterial3D
var kirmizi: StandardMaterial3D
var siyah: StandardMaterial3D


func _initialize() -> void:
	vp = SubViewport.new()
	vp.transparent_bg = true
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.msaa_3d = Viewport.MSAA_4X
	root.add_child(vp)
	var ortam := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.7, 0.74, 0.88)
	e.ambient_light_energy = 0.45
	ortam.environment = e
	vp.add_child(ortam)
	var isik := DirectionalLight3D.new()
	isik.light_energy = 0.95
	isik.rotation_degrees = Vector3(-35, 40, 0)
	isik.shadow_enabled = true
	vp.add_child(isik)
	kam = Camera3D.new()
	kam.projection = Camera3D.PROJECTION_ORTHOGONAL
	vp.add_child(kam)
	kok = Node3D.new()
	vp.add_child(kok)
	tas = _tas_malzeme(Color(0.58, 0.55, 0.5), 1)
	tas_koyu = _tas_malzeme(Color(0.42, 0.4, 0.38), 2)
	tahta = _malzeme(Color(0.52, 0.33, 0.18))
	kirmizi = _malzeme(Color(0.78, 0.16, 0.14))
	siyah = _malzeme(Color(0.08, 0.06, 0.06))
	_calis.call_deferred()


func _tas_malzeme(renk: Color, tohum: int) -> StandardMaterial3D:
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_CELLULAR
	n.cellular_return_type = FastNoiseLite.RETURN_DISTANCE2_SUB
	n.frequency = 0.035
	n.seed = tohum
	var t := NoiseTexture2D.new()
	t.width = 256
	t.height = 256
	t.seamless = true
	t.noise = n
	var g := Gradient.new()
	g.set_color(0, renk.darkened(0.45))
	g.set_color(1, renk.lightened(0.05))
	g.add_point(0.18, renk)
	t.color_ramp = g
	var m := StandardMaterial3D.new()
	m.albedo_texture = t
	m.uv1_triplanar = true
	m.uv1_scale = Vector3(0.9, 0.9, 0.9)
	m.roughness = 0.95
	await_tex(t)
	return m


func await_tex(_t: NoiseTexture2D) -> void:
	pass


func _malzeme(renk: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = renk
	m.roughness = 0.85
	return m


func _kutu(p: Vector3, s: Vector3, m: Material) -> void:
	var mi := MeshInstance3D.new()
	var b := BoxMesh.new()
	b.size = s
	mi.mesh = b
	mi.material_override = m
	mi.position = p
	kok.add_child(mi)


func _silindir(p: Vector3, ust: float, alt: float, h: float, m: Material, kenar := 16) -> void:
	var mi := MeshInstance3D.new()
	var c := CylinderMesh.new()
	c.top_radius = ust
	c.bottom_radius = alt
	c.height = h
	c.radial_segments = kenar
	mi.mesh = c
	mi.material_override = m
	mi.position = p
	kok.add_child(mi)


func _temizle() -> void:
	for c in kok.get_children():
		c.queue_free()
	kok.rotation = Vector3.ZERO
	kok.scale = Vector3.ONE


func _cek(ad: String, merkez: Vector3, gen: float, yuk: float) -> void:
	vp.size = Vector2i(int(gen * PPU), int(yuk * PPU))
	kam.size = yuk
	kam.position = Vector3(merkez.x, merkez.y, 20)
	for i in 12:
		await process_frame
	await RenderingServer.frame_post_draw
	var img := vp.get_texture().get_image()
	img.save_png("res://cikti/kale/%s.png" % ad)
	print("kale ", ad, " ", vp.size)


func _mazgal(y: float, gen: float, der: float, m: Material) -> void:
	# Kenar boyunca dişli duvar
	var adim := 0.5
	var n := int(gen / adim)
	for i in n:
		if i % 2 == 0:
			var x := -gen / 2 + adim * (i + 0.5)
			_kutu(Vector3(x, y, der / 2 - 0.15), Vector3(adim, 0.35, 0.3), m)


func _pencere(p: Vector3, gen: float, yuk: float) -> void:
	_kutu(p, Vector3(gen, yuk, 0.12), siyah)
	_silindir(p + Vector3(0, yuk / 2, 0), gen / 2, gen / 2, 0.12, siyah, 12)
	kok.get_child(kok.get_child_count() - 1).rotation_degrees.x = 90


func _calis() -> void:
	DirAccess.make_dir_recursive_absolute("res://cikti/kale")
	var donus := Vector3(0, -18, 0)

	# --- Zemin katı: kapı, meşale yerleri
	_temizle()
	_kutu(Vector3(0, 1.0, 0), Vector3(3.4, 2.0, 3.0), tas)
	_kutu(Vector3(0, 0.08, 0), Vector3(3.7, 0.16, 3.3), tas_koyu)
	_kutu(Vector3(0, 0.62, 1.5), Vector3(1.1, 1.25, 0.1), tahta)
	_silindir(Vector3(0, 1.25, 1.5), 0.55, 0.55, 0.1, tahta, 16)
	kok.get_child(kok.get_child_count() - 1).rotation_degrees.x = 90
	for x in [-0.25, 0.0, 0.25]:
		_kutu(Vector3(x, 0.8, 1.57), Vector3(0.05, 1.5, 0.05), siyah)
	_kutu(Vector3(0, 1.88, 1.52), Vector3(3.4, 0.24, 0.12), tas_koyu)
	kok.rotation_degrees = donus
	await _cek("zemin", Vector3(0, 1.05, 0), 4.4, 2.4)

	# --- Kahraman katı: balkon + pencere
	_temizle()
	_kutu(Vector3(0, 0.9, 0), Vector3(3.0, 1.8, 2.8), tas)
	_pencere(Vector3(-0.85, 1.0, 1.41), 0.42, 0.55)
	_kutu(Vector3(0, 0.1, 1.55), Vector3(3.3, 0.2, 0.9), tahta)  # balkon tabanı
	for x in [-1.5, -0.5, 0.5, 1.5]:
		_kutu(Vector3(x, -0.12, 1.75), Vector3(0.14, 0.3, 0.14), tahta)
	_kutu(Vector3(0, 1.75, 1.42), Vector3(3.1, 0.14, 0.1), tas_koyu)
	kok.rotation_degrees = donus
	await _cek("kat", Vector3(0, 0.85, 0), 4.2, 2.2)

	# --- Balkon önü korkuluğu (kahramanın önüne çizilir)
	_temizle()
	_kutu(Vector3(0, 0.1, 1.55), Vector3(3.3, 0.2, 0.9), tahta)
	for i in 7:
		_kutu(Vector3(-1.5 + i * 0.5, 0.42, 1.95), Vector3(0.12, 0.45, 0.12), tahta)
	_kutu(Vector3(0, 0.66, 1.95), Vector3(3.3, 0.1, 0.14), tahta)
	kok.rotation_degrees = donus
	await _cek("korkuluk", Vector3(0, 0.85, 0), 4.2, 2.2)

	# --- Çatı: mazgal, kule külahı, bayrak
	_temizle()
	_kutu(Vector3(0, 0.15, 0), Vector3(3.3, 0.3, 3.1), tas_koyu)
	for i in 7:
		_kutu(Vector3(-1.5 + i * 0.5, 0.5, 1.4), Vector3(0.3, 0.4, 0.3), tas if i % 2 == 0 else tas_koyu)
	_silindir(Vector3(0, 1.0, -0.3), 0.85, 0.95, 1.4, tas, 20)
	_silindir(Vector3(0, 2.4, -0.3), 0.0, 1.15, 1.4, kirmizi, 20)
	_silindir(Vector3(0, 3.6, -0.3), 0.03, 0.03, 1.1, tahta, 6)
	_kutu(Vector3(0.38, 3.95, -0.3), Vector3(0.7, 0.42, 0.03), kirmizi)
	kok.rotation_degrees = donus
	await _cek("cati", Vector3(0, 2.05, 0), 4.0, 4.4)

	# --- Ok ve balta (mermi sprite'ları)
	for a in [["ok", "res://a/arrow.gltf", Vector3(0, 0, -90), 1.4], ["balta", "res://a/axe_1handed.gltf", Vector3(0, 0, 0), 1.4]]:
		_temizle()
		var m: Node3D = (load(a[1]) as PackedScene).instantiate()
		kok.add_child(m)
		m.rotation_degrees = a[2]
		var aabb := _sinir(m)
		var mrk := aabb.get_center()
		var olcu: float = maxf(aabb.size.x, aabb.size.y) * 1.15
		await _cek(a[0], mrk, olcu, olcu)
	quit()


func _sinir(n: Node) -> AABB:
	var a := AABB()
	var ilk := true
	for mi in n.find_children("*", "MeshInstance3D", true, false):
		var b: AABB = (mi as MeshInstance3D).global_transform * (mi as MeshInstance3D).get_aabb()
		a = b if ilk else a.merge(b)
		ilk = false
	return a

-- Datos de demostración de BodegaFlow (catálogo de una bodega pequeña).
-- Es idempotente: se puede ejecutar varias veces sin duplicar filas gracias a ON CONFLICT.
-- Uso: psql "$env:ConnectionStrings__Inventory" -f database/seed-demo.sql
--
-- Cuenta incluida:
--   correo:     demo@bodegaflow.dev
--   contraseña: BodegaFlow2026!

INSERT INTO users (id, email, password_hash, created_at) VALUES
    ('01a15636-3cce-7bd3-8dc5-28ebcc0524e0', 'demo@bodegaflow.dev',
     'AQAAAAIAAYagAAAAEPoc+KzLZCZGh8s+ULN5RvzDqxm74Sh9jCT1YrcLFoq9nbpKR09EN9sR5dJvxCXaQA==',
     '2026-10-19 17:09:17.475818-05')
ON CONFLICT (email) DO NOTHING;

INSERT INTO products (id, sku, name, description, unit_price, minimum_stock, is_active, created_at) VALUES
    ('01a15636-52cf-70f4-913a-dc73d61a80ce', 'LEC-002', 'Leche entera 1 L', 'Larga vida', 4200.00, 24, true, '2026-10-19 17:09:23.150037-05'),
    ('01a15636-52f8-708c-b6a0-24b7debf27da', 'FIL-005', 'Filtro de papel #4', 'Caja x100', 9800.00, 8, false, '2026-10-19 17:09:23.191564-05'),
    ('01a15636-de9f-7b5b-a768-d890da3fa31c', 'CAF-001', 'Café tostado 500 g', 'Molido, tueste medio', 18500.00, 10, true, '2026-10-19 17:09:58.934204-05'),
    ('01a15636-deeb-7cc5-a439-480882e5e90d', 'AZU-003', 'Azúcar 1 kg', 'Refinada', 5600.00, 15, true, '2026-10-19 17:09:59.019211-05'),
    ('01a15636-deef-78a1-842c-fdbf9cbf249d', 'TAS-004', 'Taza cerámica 300 ml', 'Blanca, apta lavavajillas', 24000.00, 10, true, '2026-10-19 17:09:59.023301-05')
ON CONFLICT (sku) DO NOTHING;

INSERT INTO inventory_movements (id, product_id, type, quantity, note, user_id, created_at) VALUES
    ('82d3b7f3-c76e-4529-b9ba-28012bac437c', '01a15636-52cf-70f4-913a-dc73d61a80ce', 'entry', 40, 'Compra inicial proveedor', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:23.228606-05'),
    ('d9894ecc-1605-4979-90ba-5039e0b7a885', '01a15636-52cf-70f4-913a-dc73d61a80ce', 'exit', 5, 'Venta mostrador', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:23.244028-05'),
    ('db8d718b-9289-4bef-8cd9-be1ec0438304', '01a15636-de9f-7b5b-a768-d890da3fa31c', 'entry', 60, 'Compra inicial proveedor', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.089382-05'),
    ('9c101d36-9b01-48f8-a085-4365ed2f97f8', '01a15636-deeb-7cc5-a439-480882e5e90d', 'entry', 40, 'Compra inicial proveedor', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.106040-05'),
    ('b7e66011-39a0-426a-8588-e16ff4b769e5', '01a15636-deef-78a1-842c-fdbf9cbf249d', 'entry', 8, 'Pedido de cerámica', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.115677-05'),
    ('837d0e61-9590-469f-b939-4836e6fb59cd', '01a15636-52cf-70f4-913a-dc73d61a80ce', 'entry', 36, 'Reposición semanal', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.118152-05'),
    ('c15f0959-5515-437c-925a-406affbaaefe', '01a15636-52f8-708c-b6a0-24b7debf27da', 'entry', 20, 'Reposición semanal', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.120364-05'),
    ('6cd8982f-5e75-499d-ae5c-79d1e6384119', '01a15636-de9f-7b5b-a768-d890da3fa31c', 'exit', 12, 'Venta mostrador', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.122472-05'),
    ('d0fdb0d0-ad0b-459c-aa94-8641f02391c9', '01a15636-52cf-70f4-913a-dc73d61a80ce', 'exit', 20, 'Venta mostrador', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.126132-05'),
    ('11916051-0732-4cdd-8f47-1ad673dae38a', '01a15636-deef-78a1-842c-fdbf9cbf249d', 'exit', 4, 'Pedido cafetería', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:09:59.128505-05'),
    ('6e6510f5-d166-4dcd-b291-fbdec130a57b', '01a15636-deef-78a1-842c-fdbf9cbf249d', 'entry', 3, 'Reposición cafetería', '01a15636-3cce-7bd3-8dc5-28ebcc0524e0', '2026-10-19 17:10:59.643040-05')
ON CONFLICT (id) DO NOTHING;

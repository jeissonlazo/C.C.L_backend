-- Limpieza de datos de prueba y ajuste de un producto para mostrar el estado "bajo stock".
DELETE FROM inventory_movements WHERE note LIKE '%prueba desde UI%';
DELETE FROM products WHERE sku = 'PRU-900' AND NOT EXISTS (SELECT 1 FROM inventory_movements m WHERE m.product_id = products.id);
UPDATE products SET minimum_stock = 10 WHERE sku = 'TAS-004';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    email varchar(200) NOT NULL UNIQUE,
    password_hash varchar(500) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS products (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sku varchar(40) NOT NULL UNIQUE,
    name varchar(160) NOT NULL,
    description varchar(500),
    unit_price numeric(12,2) NOT NULL DEFAULT 0 CHECK (unit_price >= 0),
    minimum_stock integer NOT NULL DEFAULT 0 CHECK (minimum_stock >= 0),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS inventory_movements (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id uuid NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    type varchar(10) NOT NULL CHECK (type IN ('entry', 'exit')),
    quantity integer NOT NULL CHECK (quantity > 0),
    note varchar(500),
    user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_inventory_movements_created_at ON inventory_movements(created_at DESC);
CREATE INDEX IF NOT EXISTS ix_inventory_movements_product_created ON inventory_movements(product_id, created_at DESC);

-- La operación bloquea la fila del producto, valida el saldo y registra el movimiento
-- dentro de una única transacción PostgreSQL.
CREATE OR REPLACE FUNCTION register_inventory_movement(
    p_product_id uuid, p_type varchar, p_quantity integer, p_user_id uuid, p_note varchar DEFAULT NULL
)
RETURNS TABLE(id uuid, product_id uuid, product_name varchar, sku varchar, type varchar,
              quantity integer, note varchar, created_at timestamptz, error text)
LANGUAGE plpgsql AS $$
DECLARE
    current_stock bigint;
    target_product products%ROWTYPE;
    movement inventory_movements%ROWTYPE;
BEGIN
    IF p_type NOT IN ('entry', 'exit') THEN RAISE EXCEPTION 'Tipo de movimiento inválido.'; END IF;
    IF p_quantity IS NULL OR p_quantity <= 0 THEN RAISE EXCEPTION 'La cantidad debe ser mayor que cero.'; END IF;
    SELECT * INTO target_product FROM products WHERE products.id = p_product_id AND is_active = true FOR UPDATE;
    IF NOT FOUND THEN RETURN; END IF;
    IF p_type = 'exit' THEN
        SELECT COALESCE(SUM(CASE WHEN m.type = 'entry' THEN m.quantity ELSE -m.quantity END), 0)
          INTO current_stock FROM inventory_movements m WHERE m.product_id = p_product_id;
        IF current_stock < p_quantity THEN
            RETURN QUERY SELECT NULL::uuid, target_product.id, target_product.name, target_product.sku,
                p_type, p_quantity, p_note, now(), 'Existencias insuficientes para realizar la salida.'::text;
            RETURN;
        END IF;
    END IF;
    INSERT INTO inventory_movements(product_id, type, quantity, note, user_id)
    VALUES (p_product_id, p_type, p_quantity, NULLIF(p_note, ''), p_user_id) RETURNING * INTO movement;
    RETURN QUERY SELECT movement.id, target_product.id, target_product.name, target_product.sku,
        movement.type, movement.quantity, movement.note, movement.created_at, NULL::text;
END;
$$;

CREATE OR REPLACE FUNCTION get_current_inventory()
RETURNS TABLE(product_id uuid, sku varchar, name varchar, quantity bigint, minimum_stock integer,
              unit_price numeric, stock_value numeric)
LANGUAGE sql STABLE AS $$
    SELECT p.id, p.sku, p.name,
           COALESCE(SUM(CASE WHEN m.type = 'entry' THEN m.quantity ELSE -m.quantity END), 0)::bigint,
           p.minimum_stock, p.unit_price,
           (COALESCE(SUM(CASE WHEN m.type = 'entry' THEN m.quantity ELSE -m.quantity END), 0) * p.unit_price)::numeric
      FROM products p LEFT JOIN inventory_movements m ON m.product_id = p.id
     WHERE p.is_active = true GROUP BY p.id, p.sku, p.name, p.minimum_stock, p.unit_price ORDER BY p.name;
$$;

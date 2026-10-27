DO $$
BEGIN      IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = 'spike_orders') THEN
        BEGIN
          EXECUTE 'CREATE SCHEMA IF NOT EXISTS spike_orders';
        EXCEPTION
          WHEN duplicate_schema THEN NULL;
          WHEN unique_violation THEN NULL;
        END;
      END IF;

END
$$;


DO $$
BEGIN      IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace WHERE nspname = 'spike_orders') THEN
        BEGIN
          EXECUTE 'CREATE SCHEMA IF NOT EXISTS spike_orders';
        EXCEPTION
          WHEN duplicate_schema THEN NULL;
          WHEN unique_violation THEN NULL;
        END;
      END IF;

END
$$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_immutable_timestamp(value text) RETURNS timestamp without time zone LANGUAGE sql IMMUTABLE AS
$function$
select value::timestamp

$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_immutable_timestamptz(value text) RETURNS timestamp with time zone LANGUAGE sql IMMUTABLE AS
$function$
select value::timestamptz

$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_immutable_time(value text) RETURNS time without time zone LANGUAGE sql IMMUTABLE AS
$function$
select value::time

$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_immutable_date(value text) RETURNS date LANGUAGE sql IMMUTABLE AS
$function$
select value::date

$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_grams_vector(text, use_unaccent boolean DEFAULT false)
        RETURNS tsvector
        LANGUAGE plpgsql
        IMMUTABLE STRICT
AS $function$
BEGIN
RETURN (SELECT array_to_string(spike_orders.mt_grams_array($1, use_unaccent), ' ') ::tsvector);
END
$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_grams_query(text, use_unaccent boolean DEFAULT false)
        RETURNS tsquery
        LANGUAGE plpgsql
        IMMUTABLE STRICT
AS $function$
BEGIN
RETURN (SELECT array_to_string(spike_orders.mt_grams_array($1, use_unaccent), ' & ') ::tsquery);
END
$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_grams_array(words text, use_unaccent boolean DEFAULT false)
        RETURNS text[]
        LANGUAGE plpgsql
        IMMUTABLE STRICT
AS $function$
        DECLARE
result text[];
        DECLARE
word text;
        DECLARE
clean_word text;
BEGIN
                FOREACH
word IN ARRAY string_to_array(words, ' ')
                LOOP
                     clean_word = regexp_replace(spike_orders.mt_safe_unaccent(use_unaccent, word), '[^a-zA-Z0-9]+', '','g');
FOR i IN 1 .. length(clean_word)
                     LOOP
                         result := result || quote_literal(substr(lower(clean_word), i, 1));
                         result
:= result || quote_literal(substr(lower(clean_word), i, 2));
                         result
:= result || quote_literal(substr(lower(clean_word), i, 3));
END LOOP;
END LOOP;

RETURN ARRAY(SELECT DISTINCT e FROM unnest(result) AS a(e) ORDER BY e);
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_append(jsonb, text[], jsonb, boolean, jsonb default null::jsonb)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    val ALIAS FOR $3;
    if_not_exists ALIAS FOR $4;
    patch_expression ALIAS FOR $5;
    tmp_value jsonb;
BEGIN
    tmp_value = retval #> location;
    IF tmp_value IS NOT NULL AND jsonb_typeof(tmp_value) = 'array' THEN
        CASE
            WHEN NOT if_not_exists THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            WHEN patch_expression IS NULL AND jsonb_typeof(val) = 'object' AND NOT tmp_value @> jsonb_build_array(val) THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            WHEN patch_expression IS NULL AND jsonb_typeof(val) <> 'object' AND NOT tmp_value @> val THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            WHEN patch_expression IS NOT NULL AND jsonb_typeof(patch_expression) = 'array' AND jsonb_array_length(patch_expression) = 0 THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            ELSE NULL;
            END CASE;
    END IF;
    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_append_key_value(jsonb, text[], jsonb, boolean)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    val ALIAS FOR $3;
    if_not_exists ALIAS FOR $4;
    tmp_value jsonb;
    _key text;
BEGIN
    tmp_value = retval #> location;

    IF tmp_value IS NOT NULL AND jsonb_typeof(tmp_value) = 'object' THEN
        CASE
            WHEN NOT if_not_exists THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            WHEN NOT tmp_value ?| ARRAY(SELECT jsonb_object_keys(val)) THEN
                retval = jsonb_set(retval, location, tmp_value || val, FALSE);
            ELSE NULL;
        END CASE;
    END IF;
    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_copy(jsonb, text[], text[])
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    src_path ALIAS FOR $2;
    dst_path ALIAS FOR $3;
    tmp_value jsonb;
BEGIN
    tmp_value = retval #> src_path;
    retval = spike_orders.mt_jsonb_fix_null_parent(retval, dst_path);
    RETURN jsonb_set(retval, dst_path, tmp_value::jsonb, TRUE);
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_duplicate(jsonb, text[], jsonb)
RETURNS jsonb
LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    targets ALIAS FOR $3;
    tmp_value jsonb;
    target_path text[];
    target text;
BEGIN
    FOR target IN SELECT jsonb_array_elements_text(targets)
    LOOP
        target_path = spike_orders.mt_jsonb_path_to_array(target, '\.');
        retval = spike_orders.mt_jsonb_copy(retval, location, target_path);
    END LOOP;

    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_fix_null_parent(jsonb, text[])
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
retval ALIAS FOR $1;
    dst_path ALIAS FOR $2;
    dst_path_segment text[] = ARRAY[]::text[];
    dst_path_array_length integer;
    i integer = 1;
BEGIN
    dst_path_array_length = array_length(dst_path, 1);
    WHILE i <=(dst_path_array_length - 1)
    LOOP
        dst_path_segment = dst_path_segment || ARRAY[dst_path[i]];
        IF retval #> dst_path_segment IS NULL OR retval #> dst_path_segment = 'null'::jsonb THEN
            retval = jsonb_set(retval, dst_path_segment, '{}'::jsonb, TRUE);
        END IF;
        i = i + 1;
    END LOOP;

    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_increment(jsonb, text[], numeric)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
retval ALIAS FOR $1;
    location ALIAS FOR $2;
    increment_value ALIAS FOR $3;
    tmp_value jsonb;
BEGIN
    tmp_value = retval #> location;
    IF tmp_value IS NULL THEN
        tmp_value = to_jsonb(0);
END IF;

RETURN jsonb_set(retval, location, to_jsonb(tmp_value::numeric + increment_value), TRUE);
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_insert(jsonb, text[], jsonb, integer, boolean, jsonb default null::jsonb)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    val ALIAS FOR $3;
    elm_index ALIAS FOR $4;
    if_not_exists ALIAS FOR $5;
    patch_expression ALIAS FOR $6;
    tmp_value jsonb;
BEGIN
    tmp_value = retval #> location;
    IF tmp_value IS NOT NULL AND jsonb_typeof(tmp_value) = 'array' THEN
        IF elm_index IS NULL THEN
            elm_index = jsonb_array_length(tmp_value) + 1;
        END IF;
        CASE
            WHEN NOT if_not_exists THEN
                retval = jsonb_insert(retval, location || elm_index::text, val);
            WHEN patch_expression IS NULL AND jsonb_typeof(val) = 'object' AND NOT tmp_value @> jsonb_build_array(val) THEN
                retval = jsonb_insert(retval, location || elm_index::text, val);
            WHEN patch_expression IS NULL AND jsonb_typeof(val) <> 'object' AND NOT tmp_value @> val THEN
                retval = jsonb_insert(retval, location || elm_index::text, val);
            WHEN patch_expression IS NOT NULL AND jsonb_typeof(patch_expression) = 'array' AND jsonb_array_length(patch_expression) = 0 THEN
                retval = jsonb_insert(retval, location || elm_index::text, val);
            ELSE NULL;
        END CASE;
    END IF;
    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_move(jsonb, text[], text)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    src_path ALIAS FOR $2;
    dst_name ALIAS FOR $3;
    dst_path text[];
    tmp_value jsonb;
BEGIN
    tmp_value = retval #> src_path;
    retval = retval #- src_path;
    dst_path = src_path;
    dst_path[array_length(dst_path, 1)] = dst_name;
    retval = spike_orders.mt_jsonb_fix_null_parent(retval, dst_path);
    RETURN jsonb_set(retval, dst_path, tmp_value, TRUE);
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_path_to_array(text, character)
    RETURNS text[]
    LANGUAGE plpgsql
AS $function$
DECLARE
    location ALIAS FOR $1;
    regex_pattern ALIAS FOR $2;
BEGIN
RETURN regexp_split_to_array(location, regex_pattern)::text[];
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_remove(jsonb, text[], jsonb)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    val ALIAS FOR $3;
    tmp_value jsonb;
    tmp_remove jsonb;
    patch_remove jsonb;
BEGIN
    tmp_value = retval #> location;
    IF tmp_value IS NOT NULL AND jsonb_typeof(tmp_value) = 'array' THEN
        IF jsonb_typeof(val) = 'array' THEN
            tmp_remove = val;
        ELSE
            tmp_remove = jsonb_build_array(val);
        END IF;

        FOR patch_remove IN SELECT * FROM jsonb_array_elements(tmp_remove)
        LOOP
            tmp_value =(SELECT jsonb_agg(elem)
            FROM jsonb_array_elements(tmp_value) AS elem
            WHERE elem <> patch_remove);
        END LOOP;

        IF tmp_value IS NULL THEN
            tmp_value = '[]'::jsonb;
        END IF;
    END IF;
    RETURN jsonb_set(retval, location, tmp_value, FALSE);
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_remove_key(jsonb, text[], text)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    location ALIAS FOR $2;
    _key ALIAS FOR $3;
    tmp_value jsonb;
    tmp_remove jsonb;
    patch_remove jsonb;
BEGIN
    tmp_value = retval #> location;
    IF tmp_value IS NOT NULL AND jsonb_typeof(tmp_value) = 'object' THEN
        RETURN jsonb_set(retval, location, tmp_value - _key, FALSE);
    END IF;
    RETURN retval;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_jsonb_patch(jsonb, jsonb)
    RETURNS jsonb
    LANGUAGE plpgsql
AS $function$
DECLARE
    retval ALIAS FOR $1;
    patchset ALIAS FOR $2;
    patch jsonb;
    patch_path text[];
    patch_expression jsonb;
    value jsonb;
BEGIN
    FOR patch IN SELECT * from jsonb_array_elements(patchset)
    LOOP
        patch_path = spike_orders.mt_jsonb_path_to_array((patch->>'path')::text, '\.');

        patch_expression = null;
        IF (patch->>'type') IN ('remove', 'append_if_not_exists', 'insert_if_not_exists') AND (patch->>'expression') IS NOT NULL THEN
            patch_expression = jsonb_path_query_array(retval #> patch_path, (patch->>'expression')::jsonpath);
        END IF;

        CASE patch->>'type'
            WHEN 'set' THEN
                retval = jsonb_set(retval, patch_path, (patch->'value')::jsonb, TRUE);
            WHEN 'delete' THEN
                retval = retval#-patch_path;
            WHEN 'append' THEN
                retval = spike_orders.mt_jsonb_append(retval, patch_path, (patch->'value')::jsonb, FALSE);
            WHEN 'append_if_not_exists' THEN
                retval = spike_orders.mt_jsonb_append(retval, patch_path, (patch->'value')::jsonb, TRUE, patch_expression);
            WHEN 'append_key_value' THEN
                retval = spike_orders.mt_jsonb_append_key_value(retval, patch_path, (patch->'key_value')::jsonb, FALSE);
            WHEN 'append_key_value_if_not_exists' THEN
                retval = spike_orders.mt_jsonb_append_key_value(retval, patch_path, (patch->'key_value')::jsonb, TRUE);
            WHEN 'insert' THEN
                retval = spike_orders.mt_jsonb_insert(retval, patch_path, (patch->'value')::jsonb, (patch->>'index')::integer, FALSE);
            WHEN 'insert_if_not_exists' THEN
                retval = spike_orders.mt_jsonb_insert(retval, patch_path, (patch->'value')::jsonb, (patch->>'index')::integer, TRUE, patch_expression);
            WHEN 'remove' THEN
                retval = spike_orders.mt_jsonb_remove(retval, patch_path, COALESCE(patch_expression, (patch->'value')::jsonb));
            WHEN 'remove_key' THEN
                retval = spike_orders.mt_jsonb_remove_key(retval, patch_path, (patch->>'key'));
            WHEN 'duplicate' THEN
                retval = spike_orders.mt_jsonb_duplicate(retval, patch_path, (patch->'targets')::jsonb);
            WHEN 'rename' THEN
                retval = spike_orders.mt_jsonb_move(retval, patch_path, (patch->>'to')::text);
            WHEN 'increment' THEN
                retval = spike_orders.mt_jsonb_increment(retval, patch_path, (patch->>'increment')::numeric);
            WHEN 'increment_float' THEN
                retval = spike_orders.mt_jsonb_increment(retval, patch_path, (patch->>'increment')::numeric);
            ELSE NULL;
        END CASE;
    END LOOP;
    RETURN retval;
END;
$function$;


CREATE
OR REPLACE FUNCTION spike_orders.mt_safe_unaccent(use_unaccent BOOLEAN, word TEXT)
        RETURNS TEXT
        LANGUAGE plpgsql
        IMMUTABLE STRICT
AS $function$
BEGIN
IF use_unaccent THEN
    RETURN public.unaccent(word);
ELSE
    RETURN word;
END IF;
END;
$function$;


CREATE TABLE IF NOT EXISTS spike_orders.mt_doc_deadletterevent (
    id                  uuid                        NOT NULL,
    data                jsonb                       NOT NULL,
    mt_last_modified    timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
    mt_version          uuid                        NOT NULL DEFAULT (md5(random()::text || clock_timestamp()::text)::uuid),
    mt_dotnet_type      varchar                     NULL,
CONSTRAINT pkey_mt_doc_deadletterevent_id PRIMARY KEY (id)
);
CREATE TABLE IF NOT EXISTS spike_orders.mt_doc_envelope (
    id                  uuid                        NOT NULL,
    data                jsonb                       NOT NULL,
    mt_last_modified    timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
    mt_version          uuid                        NOT NULL DEFAULT (md5(random()::text || clock_timestamp()::text)::uuid),
    mt_dotnet_type      varchar                     NULL,
CONSTRAINT pkey_mt_doc_envelope_id PRIMARY KEY (id)
);
CREATE TABLE IF NOT EXISTS spike_orders.mt_doc_order (
    id                  uuid                        NOT NULL,
    data                jsonb                       NOT NULL,
    mt_last_modified    timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
    mt_dotnet_type      varchar                     NULL,
    mt_version          bigint                      NOT NULL DEFAULT 0,
CONSTRAINT pkey_mt_doc_order_id PRIMARY KEY (id)
);
CREATE TABLE IF NOT EXISTS spike_orders.mt_streams (
    id                   uuid           NOT NULL,
    type                 varchar        NULL,
    version              bigint         NULL,
    timestamp            timestamptz    NOT NULL DEFAULT (now()),
    created              timestamptz    NOT NULL DEFAULT (now()),
    tenant_id            varchar        NULL DEFAULT '*DEFAULT*',
    compacted_version    bigint         NOT NULL DEFAULT 0,
    is_archived          bool           NULL DEFAULT FALSE,
CONSTRAINT pkey_mt_streams_id PRIMARY KEY (id)
);
CREATE TABLE IF NOT EXISTS spike_orders.mt_events (
    seq_id            bigint                      NOT NULL,
    id                uuid                        NOT NULL,
    stream_id         uuid                        NULL,
    version           bigint                      NOT NULL,
    data              jsonb                       NOT NULL,
    bdata             bytea                       NULL,
    type              varchar(500)                NOT NULL,
    timestamp         timestamp with time zone    NOT NULL DEFAULT '(now())',
    tenant_id         varchar                     NULL DEFAULT '*DEFAULT*',
    mt_dotnet_type    varchar                     NULL,
    is_archived       bool                        NULL DEFAULT FALSE,
CONSTRAINT pkey_mt_events_seq_id PRIMARY KEY (seq_id)
);

DO $do$
BEGIN
ALTER TABLE spike_orders.mt_events
ADD CONSTRAINT fkey_mt_events_stream_id FOREIGN KEY(stream_id)
REFERENCES spike_orders.mt_streams(id)ON DELETE CASCADE
;
EXCEPTION
    WHEN duplicate_object THEN NULL;
END
$do$;

CREATE UNIQUE INDEX pk_mt_events_stream_and_version ON spike_orders.mt_events USING btree (stream_id, version);
CREATE SEQUENCE spike_orders.mt_events_sequence;
ALTER SEQUENCE spike_orders.mt_events_sequence OWNED BY spike_orders.mt_events.seq_id;
CREATE TABLE IF NOT EXISTS spike_orders.mt_event_progression (
    name                         varchar                     NOT NULL,
    last_seq_id                  bigint                      NULL,
    last_updated                 timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
    mode                         varchar                     NULL DEFAULT 'none',
    rebuild_threshold            integer                     NULL DEFAULT 0,
    assigned_node                integer                     NULL DEFAULT 0,
    heartbeat                    timestamp with time zone    NULL,
    agent_status                 varchar(20)                 NULL,
    pause_reason                 text                        NULL,
    running_on_node              integer                     NULL,
    warning_behind_threshold     bigint                      NULL,
    critical_behind_threshold    bigint                      NULL,
    failure_category             varchar(50)                 NULL,
    failure_event_sequence       bigint                      NULL,
    failure_event_type           varchar(500)                NULL,
    failure_event_tenant_id      varchar(500)                NULL,
CONSTRAINT pk_mt_event_progression PRIMARY KEY (name)
);
CREATE TABLE IF NOT EXISTS spike_orders.mt_event_projection_floors (
    name            varchar                     NOT NULL,
    tenant_id       varchar                     NOT NULL,
    floor           bigint                      NOT NULL,
    last_updated    timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
CONSTRAINT pkey_mt_event_projection_floors_name_tenant_id PRIMARY KEY (name, tenant_id)
);
CREATE
OR REPLACE FUNCTION spike_orders.mt_mark_event_progression(name varchar, last_encountered bigint) RETURNS VOID LANGUAGE plpgsql AS
$function$
BEGIN
INSERT INTO spike_orders.mt_event_progression (name, last_seq_id, last_updated)
VALUES (name, last_encountered, transaction_timestamp())
ON CONFLICT ON CONSTRAINT pk_mt_event_progression
    DO
UPDATE SET last_seq_id = last_encountered, last_updated = transaction_timestamp();

END;

$function$;



CREATE OR REPLACE FUNCTION spike_orders.mt_archive_stream(streamid uuid) RETURNS VOID LANGUAGE plpgsql AS
$function$
BEGIN
  update spike_orders.mt_streams set is_archived = TRUE where id = streamid ;
  update spike_orders.mt_events set is_archived = TRUE where stream_id = streamid ;
END;
$function$;


CREATE OR REPLACE FUNCTION spike_orders.mt_quick_append_events(stream uuid, stream_type varchar, tenantid varchar, event_ids uuid[], event_types varchar[], dotnet_types varchar[], bodies jsonb[], bdatas bytea[], timestamps timestamp with time zone[], expected_version bigint DEFAULT NULL::bigint) RETURNS bigint[] AS $$
DECLARE
	event_version bigint;
	stream_is_archived boolean;
	event_type varchar;
	event_id uuid;
	body jsonb;
	index int;
	seq bigint;
    actual_tenant varchar;
    is_new_stream boolean := false;
	return_value bigint[];
BEGIN
    if expected_version IS NOT NULL then
        -- COALESCE turns the NULL we get for a brand-new stream into 0, so a
        -- FetchForWriting against a non-existent stream (which sets
        -- ExpectedVersionOnServer = 0) and a StartStream(id, version: 0) both
        -- land on the new-stream branch instead of mis-firing the guard.
        select version, is_archived into event_version, stream_is_archived from spike_orders.mt_streams where id = stream;
        if COALESCE(event_version, 0) != expected_version then
            RAISE EXCEPTION 'Stream version mismatch on ''%'': expected %, actual %', stream, expected_version, COALESCE(event_version, 0) USING ERRCODE = 'MT003';
        end if;
    else
        select version, is_archived into event_version, stream_is_archived from spike_orders.mt_streams where id = stream;
    end if;

	if event_version IS NULL then
		event_version = 0;
		is_new_stream := true;
		-- #5456 follow-up: insert the FINAL version straight away rather than 0 plus a
		-- trailing UPDATE of the row we just wrote in this same transaction. That UPDATE
		-- is a second heap tuple + WAL record per stream creation for no observable gain
		-- -- nothing outside this transaction can see the intermediate 0. The trailing
		-- UPDATE below is skipped for this branch via is_new_stream.
		insert into spike_orders.mt_streams (id, type, version, timestamp, tenant_id) values (stream, stream_type, COALESCE(array_length(event_ids, 1), 0), now(), tenantid);
    else
        if stream_is_archived then
            RAISE EXCEPTION 'Attempted to append event to archived stream with Id ''%''.', stream USING ERRCODE = 'MT001';
        end if;
        if tenantid IS NOT NULL then
            select tenant_id into actual_tenant from spike_orders.mt_streams where id = stream;
            if actual_tenant != tenantid then
                RAISE EXCEPTION 'The tenantid does not match the existing stream';
            end if;
        end if;
	end if;

	index := 1;
	-- #5062: array_length('{}', 1) is NULL in PostgreSQL, not 0, so a call with an
	-- empty event array used to return ARRAY[NULL] -- a bigint[] whose only element
	-- is NULL, which Npgsql cannot read into long[] ('Cannot read a non-nullable
	-- collection of elements because the returned array contains nulls'). COALESCE
	-- makes the empty case mean what it says: zero events appended, so the final
	-- version is the stream's current version.
	return_value := ARRAY[event_version + COALESCE(array_length(event_ids, 1), 0)];

	foreach event_id in ARRAY event_ids
	loop
        seq := nextval('spike_orders.mt_events_sequence');
		return_value := array_append(return_value, seq);

	    event_version := event_version + 1;
		event_type = event_types[index];
		body = bodies[index];

		-- #4515 / #4578 / Phase 2: bdatas[index] carries the binary payload
		-- for events opted in to binary serialization (NULL otherwise).
		-- bodies[index] is the {} JSON placeholder for those events so the
		-- existing data jsonb NOT NULL constraint stays intact.
		insert into spike_orders.mt_events
			(seq_id, id, stream_id, version, data, bdata, type, tenant_id, timestamp, mt_dotnet_type, is_archived)
		values
			(seq, event_id, stream, event_version, body, bdatas[index], event_type, tenantid, timestamps[index], dotnet_types[index], FALSE);

		index := index + 1;
	end loop;

	-- A brand-new stream already carries its final version from the insert above, so the
	-- UPDATE is only needed when we appended onto a stream that already existed.
	--
	-- #5539: and only when we actually appended something. With ZERO events this was a BLIND write-back
	-- of the version read at the top of the function, and under READ COMMITTED that is a lost update: the
	-- UPDATE waits on a concurrent appender's row lock, the winner commits, the WHERE clause is
	-- re-evaluated against the new row, and `set version = <the stale value>` lands -- leaving
	-- mt_streams.version BELOW the stream's own events. Every later append then computes a version that
	-- already exists and dies on pk_mt_events_stream_and_version, forever, until the row is repaired by
	-- hand. A non-empty call cannot do this: its first INSERT collides with the winner on that same
	-- primary key and rolls the whole thing back, which is why only the empty case was exposed.
	--
	-- Two independent guards, because either alone would have been enough and the cost of both is nil:
	--   * the append count -- appending nothing has nothing to write, so the correct number of statements
	--     is zero rather than one that happens to be harmless;
	--   * greatest(), so the write-back can never LOWER a stream's version whoever calls it. An append
	--     only ever moves a version forward, so this is a no-op on every correct path and a backstop on
	--     any future caller that reads the version and then writes it back.
	if not is_new_stream and COALESCE(array_length(event_ids, 1), 0) > 0 then
		update spike_orders.mt_streams set version = greatest(version, event_version), timestamp = now() where id = stream;
	end if;

	return return_value;
END
$$ LANGUAGE plpgsql;

CREATE TABLE IF NOT EXISTS spike_orders.mt_high_water_skips (
    ending_sequence      bigint                      NOT NULL,
    starting_sequence    bigint                      NOT NULL,
    timestamp            timestamp with time zone    NULL DEFAULT (transaction_timestamp()),
CONSTRAINT pkey_mt_high_water_skips_ending_sequence PRIMARY KEY (ending_sequence)
);
CREATE
OR REPLACE FUNCTION spike_orders.mt_mark_progression_with_skip(shard_name varchar, ending_sequence bigint, starting_sequence bigint) RETURNS bigint AS
$$
DECLARE
    current_value bigint;
BEGIN
    select last_seq_id into current_value from spike_orders.mt_event_progression where name = shard_name;

    IF current_value is null then
        -- Bootstrap the high-water row on first invocation. Previously this branch
        -- returned 0, which left HighWaterAgent.CheckNowAsync polling forever because
        -- statistics.CurrentMark never advanced. Issue #4425.
        INSERT INTO spike_orders.mt_event_progression (name, last_seq_id, last_updated)
        VALUES (shard_name, ending_sequence, transaction_timestamp())
        ON CONFLICT ON CONSTRAINT pk_mt_event_progression
            DO UPDATE SET last_seq_id = ending_sequence, last_updated = transaction_timestamp();
        IF ending_sequence > starting_sequence THEN
            -- Idempotent: a rebuild/replay re-marks the same skip (ending_sequence is the
            -- PK). Re-recording an existing gap is a no-op. Conflict target is the PK
            -- constraint (the bare column name collides with the function parameter). #4530.
            insert into spike_orders.mt_high_water_skips (ending_sequence, starting_sequence) values (ending_sequence, starting_sequence)
                on conflict on constraint pkey_mt_high_water_skips_ending_sequence do nothing;
        END IF;
        return ending_sequence;
    ELSIF current_value = starting_sequence THEN
        update spike_orders.mt_event_progression SET last_seq_id = ending_sequence, last_updated = transaction_timestamp() where shard_name = name;
        insert into spike_orders.mt_high_water_skips (ending_sequence, starting_sequence) values (ending_sequence, starting_sequence)
            on conflict on constraint pkey_mt_high_water_skips_ending_sequence do nothing;
        return ending_sequence;
    ELSE
        return current_value;
    END IF;
END
$$
LANGUAGE plpgsql;



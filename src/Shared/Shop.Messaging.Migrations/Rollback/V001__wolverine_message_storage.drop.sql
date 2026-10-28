DO LANGUAGE plpgsql $tran$
BEGIN

DROP TABLE IF EXISTS wolverine.wolverine_outgoing_envelopes CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_incoming_envelopes CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_dead_letters CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_dead_letter_history CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_nodes CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_node_assignments CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_control_queue CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_node_records CASCADE;
DROP TABLE IF EXISTS wolverine.wolverine_agent_restrictions CASCADE;

END;
$tran$;

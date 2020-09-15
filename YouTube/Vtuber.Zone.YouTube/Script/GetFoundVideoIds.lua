redis.replicate_commands()

redis.call("DELETE", @tmp_key)

local cursor = "0"
repeat
    local keys
    cursor, keys = unpack(redis.call("SCAN", cursor, "MATCH", @found_ids_key_pattern))
    for _, key in ipairs(keys) do
        local ids = redis.call("ZRANGE", key, 0, -1)
        local args = {"SADD", @tmp_key, unpack(ids)}
        redis.call(unpack(args))
    end
until cursor == "0"

return redis.call("SDIFF", @tmp_key, @bad_ids_key)

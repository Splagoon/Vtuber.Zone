redis.replicate_commands()

if redis.call("EXISTS", @key) == 0 then
    local cursor = "0"
    local all_keys = {}
    repeat
        local keys
        cursor, keys = unpack(redis.call("SCAN", cursor, "MATCH", @key_pattern))
        for _, k in ipairs(keys) do
            all_keys[#all_keys+1] = k
        end
    until cursor == "0"
    if #all_keys == 0 then
        return nil
    end
    local args = {"ZUNIONSTORE", @key, #all_keys, unpack(all_keys)}
    redis.call(unpack(args))
end

return redis.call("ZREVRANGE", @key, 0, -1)

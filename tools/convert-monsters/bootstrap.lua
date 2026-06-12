-- Sandbox bootstrap for converting Canary monster files.
-- Unknown globals (COMBAT_*, CONST_ME_*, ...) resolve to their own name as a string.
__registered = nil
Game = {
  createMonsterType = function(name)
    local mt = { __name = name }
    mt.register = function(self, monster)
      monster.__name = self.__name
      __registered = monster
    end
    return setmetatable(mt, { __index = function() return function() end end })
  end
}
setmetatable(_G, { __index = function(t, k) return k end })

local function esc(s)
  s = s:gsub("\\", "\\\\")
  s = s:gsub('"', '\\"')
  s = s:gsub("\n", "\\n")
  s = s:gsub("\r", "\\r")
  s = s:gsub("\t", "\\t")
  s = s:gsub("%c", "")
  return '"' .. s .. '"'
end

function __tojson(v, depth)
  depth = depth or 0
  if depth > 8 then return "null" end
  local t = type(v)
  if t == "number" then
    if v ~= v or v == math.huge or v == -math.huge then return "0" end
    return string.format("%.10g", v)
  elseif t == "boolean" then
    return tostring(v)
  elseif t == "string" then
    return esc(v)
  elseif t == "table" then
    local hasStringKeys = false
    for k in pairs(v) do
      if type(k) ~= "number" then hasStringKeys = true break end
    end
    local arrayPart = {}
    for i = 1, #v do arrayPart[#arrayPart + 1] = __tojson(v[i], depth + 1) end
    if not hasStringKeys then
      return "[" .. table.concat(arrayPart, ",") .. "]"
    end
    local parts = {}
    if #arrayPart > 0 then
      parts[#parts + 1] = '"__items":[' .. table.concat(arrayPart, ",") .. "]"
    end
    for k, val in pairs(v) do
      if type(k) == "string" and type(val) ~= "function" then
        parts[#parts + 1] = esc(k) .. ":" .. __tojson(val, depth + 1)
      end
    end
    return "{" .. table.concat(parts, ",") .. "}"
  end
  return "null"
end

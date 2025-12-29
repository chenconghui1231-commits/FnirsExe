% convert_mat_files.m - 转换MAT文件格式以提高兼容性
% 这是一个脚本文件，不是函数文件

fprintf('开始转换MAT文件格式...\n');

% 设置文件路径
input_dir = 'E:\source\FnirsExe\FnirsExe\Resources';
output_dir = input_dir; % 输出到同一目录

fprintf('输入目录: %s\n', input_dir);

% 获取目录下所有.mat文件
mat_files = dir(fullfile(input_dir, '*.mat'));

if isempty(mat_files)
    fprintf('未找到.mat文件！\n');
    return;
end

fprintf('找到 %d 个MAT文件:\n', length(mat_files));
for i = 1:length(mat_files)
    fprintf('  %d. %s\n', i, mat_files(i).name);
end

% 转换每个文件
for i = 1:length(mat_files)
    original_file = fullfile(input_dir, mat_files(i).name);
    [filepath, name, ext] = fileparts(original_file);
    converted_file = fullfile(output_dir, [name '_converted' ext]);
    
    fprintf('\n正在转换: %s\n', mat_files(i).name);
    
    try
        % 显示原文件信息
        file_info = whos('-file', original_file);
        fprintf('原文件变量:\n');
        for j = 1:length(file_info)
            fprintf('  - %s: %s [%dx%d]\n', ...
                file_info(j).name, ...
                file_info(j).class, ...
                file_info(j).size(1), ...
                file_info(j).size(2));
        end
        
        % 加载原文件
        loaded_data = load(original_file);
        
        % 保存为v7格式（更好的兼容性）
        save(converted_file, '-struct', 'loaded_data', '-v7');
        
        fprintf('✓ 成功转换: %s -> %s\n', mat_files(i).name, [name '_converted' ext]);
        
    catch ME
        fprintf('✗ 转换失败: %s\n', ME.message);
        
        % 尝试备选方案：分别保存每个变量
        try
            fprintf('尝试备选转换方案...\n');
            variable_names = fieldnames(loaded_data);
            
            % 使用动态变量保存
            save_cmd = 'save(converted_file';
            for k = 1:length(variable_names)
                var_name = variable_names{k};
                assignin('base', var_name, loaded_data.(var_name));
                save_cmd = [save_cmd ', ''' var_name ''''];
            end
            save_cmd = [save_cmd ', ''-v7'');'];
            
            eval(save_cmd);
            fprintf('✓ 备选方案成功: %s\n', [name '_converted' ext]);
            
        catch ME2
            fprintf('✗ 备选方案也失败: %s\n', ME2.message);
        end
    end
end

fprintf('\n转换完成！\n');
fprintf('请在你的C#代码中使用 *_converted.mat 文件\n');
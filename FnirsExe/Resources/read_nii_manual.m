function [data, header] = read_nii_manual(filename)
    % 手动读取 NIfTI 文件
    % 输入: filename - NIfTI 文件路径
    % 输出: data - 图像数据, header - 头文件信息
    
    fid = fopen(filename, 'r', 'ieee-be');
    if fid == -1
        error('无法打开文件: %s', filename);
    end
    
    fprintf('正在读取文件: %s\n', filename);
    
    % 读取头文件基本信息
    header.sizeof_hdr = fread(fid, 1, 'int32');
    header.dim_info = fread(fid, 1, 'char');
    header.dim = fread(fid, 8, 'int16');
    header.intent_p1 = fread(fid, 1, 'float32');
    header.intent_p2 = fread(fid, 1, 'float32');
    header.intent_p3 = fread(fid, 1, 'float32');
    header.intent_code = fread(fid, 1, 'int16');
    header.datatype = fread(fid, 1, 'int16');
    header.bitpix = fread(fid, 1, 'int16');
    header.slice_start = fread(fid, 1, 'int16');
    header.pixdim = fread(fid, 8, 'float32');
    header.vox_offset = fread(fid, 1, 'float32');
    header.scl_slope = fread(fid, 1, 'float32');
    header.scl_inter = fread(fid, 1, 'float32');
    header.slice_end = fread(fid, 1, 'int16');
    header.slice_code = fread(fid, 1, 'char');
    header.xyzt_units = fread(fid, 1, 'char');
    
    % 移动到数据开始位置
    fseek(fid, header.vox_offset, 'bof');
    
    % 计算数据大小
    data_dims = header.dim(2:header.dim(1)+1);
    num_elements = prod(data_dims);
    
    % 根据数据类型读取数据
    switch header.datatype
        case 2  % DT_UNSIGNED_CHAR
            data = fread(fid, num_elements, 'uint8=>float32');
        case 4  % DT_SIGNED_SHORT
            data = fread(fid, num_elements, 'int16=>float32');
        case 8  % DT_SIGNED_INT
            data = fread(fid, num_elements, 'int32=>float32');
        case 16 % DT_FLOAT
            data = fread(fid, num_elements, 'float32=>float32');
        case 64 % DT_DOUBLE
            data = fread(fid, num_elements, 'float64=>float32');
        otherwise
            fclose(fid);
            error('不支持的数据类型: %d', header.datatype);
    end
    
    fclose(fid);
    
    % 重塑数据维度
    data = reshape(data, data_dims');
    
    fprintf('读取成功! 数据维度: %s\n', mat2str(size(data)));
end